using DapperMicroOrm.Models;
using System.Linq.Expressions;

namespace DapperMicroOrm.Services;
public interface IDapperService<TEntity> where TEntity : class
{
    Task InsertAsync(TEntity entity);
    Task BulkInsertAsync(IEnumerable<TEntity> entities);
    Task<bool> UpdateAsync(TEntity entity);
    Task<IEnumerable<TEntity>> GetAllAsync(Expression<Func<TEntity, bool>> expression = null);
    Task<TEntity> GetAsync(Expression<Func<TEntity, bool>> expression);
    Task<bool> Delete(TEntity entity);
}
