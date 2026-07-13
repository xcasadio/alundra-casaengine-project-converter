using Xunit;

// CasaEngine's EngineEnvironment.ProjectPath and AssetCatalog are process-global statics; tests
// that mutate them must not run concurrently.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
