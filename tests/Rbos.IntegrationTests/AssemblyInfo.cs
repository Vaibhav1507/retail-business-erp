using Xunit;

// Each integration-test class creates its own WebApplicationFactory and Npgsql pools.
// Running many hosts concurrently exhausts PostgreSQL's CI connection limit.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
