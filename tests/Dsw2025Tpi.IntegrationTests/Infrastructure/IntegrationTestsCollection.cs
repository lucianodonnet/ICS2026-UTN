namespace Dsw2025Tpi.IntegrationTests.Infrastructure;

// Todas las clases de esta colección comparten la misma fábrica (y la misma base SQLite)
// y se ejecutan en secuencia, sin colisiones de concurrencia.
[CollectionDefinition(Name)]
public class IntegrationTestsCollection : ICollectionFixture<CustomWebApplicationFactory>
{
    public const string Name = "IntegrationTests";
}
