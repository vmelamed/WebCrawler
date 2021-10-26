# Web Crawler Architecture

*Web crawler* or *spider* is a software system that downloads documents from the internet and indexes their content for general words, or media specific "tokens" (images, video, etc.), or some content specific data (e.g. medical terms). The output should is a documents index to be available to internet search API-s. The document parser should be able to generate a few streams of tokens: search API specific tokens (e.g. words), URI-s, and possibly policy updating tokens (e.g. tokens generated by parsing `robot.txt`, HTTP headers, and other *policies* related content). The stream of URI-s should be used to download and parse their respective documents, etc. One can think of the process as traversing a huge graph of documents, hence the metaphor of "spider walking the web". The policies are data driven modifiers (basic example might be a *decorator* postponing the next download). The policies implement the crawler's compliance with laws, internal rules, site settings (`robot.txt`), politeness, etc. rules.

## General Notes on the proposed architecture

See alsothe diagrams:

- [Component  diagram](./Components.pdf)
- [UriState FS Diagram](./States.pdf)

### Transport, Serialization, IPC

The crawler is distributed system employing containerization technologies (Docker, Kubernetes, Azure Service Fabric, etc.) The containers contain mostly highly efficient, stateless services communicating over fast internal transports, serialization standards and IPC mechanisms:

- the serialization mechanisms should be fast, e.g. protobuf, MessagePack or even proprietary binary serialization mechanisms
- the IPC mechanisms also should be chosen with performance in mind, e.g. gRPC or possibly custom implementations
- the component diagram shows a number of queues. From programming point of view these should be considered transport mechanism aiding one-way/fire-and-forget/queued-event type of IPC.
  The queues can be part of a more advanced queueing/event managing system, e.g. service bus (pub/sub) subject to performance and volume considerations. First choice of technologies that comes to mind are: Redis queues (preferably enterprize edition - with persistance to fast media); Kafka for large amounts of data (e.g. the staging area that transfers the parsed content to the indexing engine)

### Clusters

It is proposed that there should be two types of clusters:

#### Main or general cluster

This cluster contain the services that implement the management and support interfaces. It also contains the databases with centralized dita:

##### Global Databases

- **`GlobalURIs`**: transactional database that maintains the states of the processed URI-s in the global scope. It supports mostly the crawler management interfaces
- **`GlobalDNS`**: eliminates the need to call DNS for each URI. Requires regular maintenance and host names update jobs
- **`URIAnalytics`** the crawler may include analytical database (Hadoop, Vertica, etc.). They are slower to process transactions (create or update) but highly performant for analytics on a large scale used by some of the policies; reports; etc. It can even serve some ML engine that has the ability to adjust download and retention policies

##### Global Services

- `Management Classes` implement the crawler's management and visualization functions. In particular, there must be interfaces that manage the state of URI-s - `IManageUriTasks` (almost all interfaces and implementations are asynchronous for better resource utilization). These are basic CRUD operations that allow to:
  - enter seed URI-s (URI-s that are not in the system and are not referred to by any of the managed URI-s so far)
  - manual update of URI download status (to and from `UriStatus.Suspended` state)
  - remove sites and URI-s from the system
  - get a list of or a single instance of `UriState`
- `Updater`: handles the `Update(UriStatus)` events by implementing the `UriStatus` state transitioning and pre- and post-state actions: e.g. every state update should also be sent to the `URIAnalytics`. The service is stateless and there can be a number of instances. As a general principal the number of instances should be a function of and compromise between throughput/availability/scalability/performance: too many services concurrently updating the DB may lead to contention for DB resources or lead to race conditions and other adverse effects (e.g. deadlocks).

#### Local Clusters

To improve the scalability of the system, we split the data and the download/parsing operations into several identical local clusters by geographical and network criteria. The clusters should be physically located in the corresponding (cloud?) regions. The `GlobalURIs` and the `GlobalDNS` databases determine the assignment of the URI-s to the clusters also based on their geographical and network closeness.

##### Local Databases

- **`URIs`** is a local view of `GlobalURI` - the URI-s that which hosts reside in the cluster's region. Since the clusters are geographically spread, there might be a undesirable latency. There is a component that is not shown on the diagram - the one that implements the view replication of the data from the global to/from the local DB servers. It plays important role when seed URI-s are added to the global DB. The replication components must assign the `UriState` instances to a cluster (`UriState.Cluster`) and then replicate it to the local DB `URIs`. Another important role of the replicator is in disaster recovery scenarios.
- **`LocalDNS`** is a local view of the `GlobalDNS`.

##### Local Services

Most of the services here are single-function, stateless implementations, connected via queues/service bus, unless explicitly noted. Because the services are stateless, there can be multiple load balanced instances of each class for better scalability and throughput. The best description of the services would be, if we follow the URI flow through the system:

1. From UI, or some import mechanism, a new URI is entered in the system by an instance from the global cluster implementing `IMangeUriTasks` - the management interfaces of the main cluster. The service creates a new `UriState` object (lets call it ****X****) with status `UriStatus.Created` and pushes it to the `GlobalUpdate` queue (publishes `GlobalUpdate` event).
1. One of the global `Updater` instances (subscribed to `GlobalUpdate` queue)
   1. pops the new `UriState` **X** from the queue (or the event)
   1. resolves and populates the host IP address in the field `UriState.HostIpAddress` from the `GlobalDNS` database. If the address is not in the `GlobalDNS` the `Updater` should resolve it from the DNS service, and trigger an update to the `GlobalDNS` with the resolved host address
   1. assign the **X** instance to a cluster - update the field `UriState.Cluster`
   1. fires `Update(UriState)` to the local `Schedule` queue in the assigned cluster
   1. finally persists the object in the `GlobalURIs` DB
    In general, the main responsibilities of the global and local `Updater` instances are to implement the various state transitions or to follows the state diagram:
      - make sure that the requested move to the target state is a valid transition
      - to update at least the fields `UriState.Status` and  `UriState.LastUpdate` on every state transition
      - implement other transition-specific updates of the `UriState` objects (e.g. update the `HostIpAddress`)
      - replicate the local event `Update(UriState)` to the `GlobalUpdate` queue (in order to keep the global data in sync with the local)
1. The events (`UriState` objects) in the `Schedule` queue are picked-up by one of the `Scheduler` instances
   1. if the scheduler finds that the created `UriState` object already exists in the URIs DB - it ignores (throws it away)
   1. by applying the applicable policies, the scheduler computes when to download from the URI - `UriState.DownloadAt`
   1. transition the state by firing `Update(UriState, UriStatus.Scheduled)` event to the `UpdateQueue`
   1. alternatively, the `Scheduler` may decide that the URI should not be visited - fire `Update(UriState, UriStatus.Suspended)`
   1. the updater stores or updates **X** in the Local DB
1. The `UriState` objects that are scheduled to be downloaded are cached in a high performance in-memory cache. The `CacheUpdater` is a service (can be singleton) that is executed periodically on timer (e.g. chron job) to refresh the `URI Cache`. The `CacheUpdater` may also be activated by directly invoking its interface `IUpdateUriCache` when the cache is exhausted. The `CacheUpdater` reads from `URIs` DB all `UriState` objects that are in `UriStatus.Scheduled` and where `UriState.DownloadAt <= <now>+<refresh cycle interval>`.
1. The `Activator`
   1. loads the `UriState` objects from the `URICache`, sorted by `DownloadAt` starting from the oldest
   1. pushes the loaded objects to the download queue
   1. removes the loaded from the cache
   1. fires `Update(UriState, UriStatus.Downloading)` for all of the loaded `UriState`-s
   1. if the cache is exhausted, calls `IRefreshUriCache`
1. A `Downloader` instance will pick up the `UriState` from the download queue and download the document from the internet, updating:
   - `UriState.DownloadStarted`
   - `UriState.DownloadFinished`
   - `UriState.LastHttpStatus`
   1. fire `Update(UriState, UriStatus.Parsing)`
1. To minimize the transport of possibly large documents across process/container/pod boundaries, the service will pass the `UriState` object and the corresponding downloaded document over straight forward .NET interface to the `Parser` object inside the container.
   1. Computes and updates the field `UriState.Hash`, if the hash is the same, the parser fires `Update(UriState, UriStatus.Parsed)`; puts the `UriState` object into the `Schedule` queue and returns, otherwise continues with parsing the document
   1. The parser may update some policies depending on headers and or predefined conventions (`robot.txt`)
   1. While parsing the document, the parser creates a new `UriState` object for each hyperlink on the page and fires `Update(UriState, UriStatus.Created)`
   1. The parser stores the document tokens and the URI in the staging area (could be as simple as a flat file) and the indexer will pick it up from there.
1. This is where we are back to #3.

The process of the `UriState` **X** has come full circle. The scheduler will schedule it for new download and index refresh in possibly a few weeks (policy).
