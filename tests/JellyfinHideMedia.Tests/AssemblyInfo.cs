// Plugin.Instance and the loaded JavaScript Injector are process-wide, so tests must not run concurrently.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
