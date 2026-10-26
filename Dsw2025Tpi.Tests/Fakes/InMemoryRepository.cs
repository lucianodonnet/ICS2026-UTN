using System.Linq.Expressions;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Domain.Interfaces;

namespace Dsw2025Tpi.Tests.Fakes;

// Repositorio en memoria para probar los servicios sin base de datos.
public class InMemoryRepository : IRepository
{
    private readonly List<EntityBase> _entities = [];

    public InMemoryRepository(params EntityBase[] seed)
    {
        _entities.AddRange(seed);
    }

    public IReadOnlyList<EntityBase> Entities => _entities;

    public Task<T?> GetById<T>(Guid id, params string[] include) where T : EntityBase
        => Task.FromResult(_entities.OfType<T>().FirstOrDefault(e => e.Id == id));

    public Task<IEnumerable<T>?> GetAll<T>(params string[] include) where T : EntityBase
        => Task.FromResult<IEnumerable<T>?>(_entities.OfType<T>().ToList());

    public Task<T?> First<T>(Expression<Func<T, bool>> predicate, params string[] include) where T : EntityBase
        => Task.FromResult(_entities.OfType<T>().FirstOrDefault(predicate.Compile()));

    public Task<IEnumerable<T>?> GetFiltered<T>(Expression<Func<T, bool>> predicate, params string[] include) where T : EntityBase
        => Task.FromResult<IEnumerable<T>?>(_entities.OfType<T>().Where(predicate.Compile()).ToList());

    public Task<T> Add<T>(T entity) where T : EntityBase
    {
        _entities.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<T> Update<T>(T entity) where T : EntityBase => Task.FromResult(entity);

    public Task<T> Delete<T>(T entity) where T : EntityBase
    {
        _entities.Remove(entity);
        return Task.FromResult(entity);
    }
}
