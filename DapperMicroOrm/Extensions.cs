using PluralizeService.Core;
using System.Data.SqlClient;
using System.Data;

namespace DapperMicroOrm;

public static class Extensions
{
    public static string GetTableName<TEntity>(this TEntity entity)
    {
        return PluralizationProvider.Pluralize(entity.GetType().Name);
    }

    public static string GetTableName<TEntity>(this IEnumerable<TEntity> entities)
    {
        return PluralizationProvider.Pluralize(entities.FirstOrDefault().GetType().Name);
    }
}
