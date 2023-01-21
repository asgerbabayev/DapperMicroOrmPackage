using Dapper;
using DapperMicroOrm.Models;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.SqlClient;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using static Dapper.SqlMapper;

namespace DapperMicroOrm.Services;

public class DapperManager<TEntity> : IDapperService<TEntity>
    where TEntity : class, new()
{
    private readonly string _connectionString;
    public DapperManager(string connectionString) =>
        this._connectionString = connectionString;

    public async Task<bool> Delete(TEntity entity)
    {
        string table = entity.GetTableName();
        var id = entity.GetType().GetProperty("Id").GetValue(entity).ToString();
        using (IDbConnection connection = new SqlConnection(_connectionString))
        {
            bool result = await connection.ExecuteAsync($"DELETE FROM {table} WHERE Id = @Id", new { Id = id }) > 0;
            return result;
        }
    }
    public async Task<TEntity> GetAsync(Expression<Func<TEntity, bool>> expression)
    {
        TEntity entity = new();
        string table = entity.GetTableName();
        StringBuilder query = new();
        foreach (PropertyInfo prop in typeof(TEntity).GetProperties())
        {
            query.Append("[").Append(prop.Name).Append("]");
            if (prop != typeof(TEntity).GetProperties()[^1]) query.Append(", ");
        }
        using (IDbConnection connection = new SqlConnection(_connectionString))
        {
            StringBuilder param = new();
            StringBuilder vals = new();
            string parameters = string.Empty;
            var values = new DynamicParameters();
            var list = CheckConditions(new HashSet<Condition>(), expression.Body, "");
            foreach (var item in list)
            {
                param.Append($"[{item.Name}] = @{item.Name} {item.Operator} ");
                vals.Append(@$"@{item.Name} = ""{item.Value}"", ");
                values.Add($"@{item.Name}", item.Value);
            }
            if (param.ToString().EndsWith("And ")) parameters = param.ToString().TrimEnd('d', 'n', 'A', ' ', ',');
            else if (param.ToString().EndsWith("Or ")) parameters = param.ToString().TrimEnd('r', 'O', ' ', ',');
            else parameters = param.ToString().TrimEnd(' ');
            string rawQuery = $"Select {query} From {table} Where {parameters}";
            TEntity result = await connection.QueryFirstOrDefaultAsync<TEntity>(rawQuery, values);
            return result;
        }
    }
    public async Task<IEnumerable<TEntity>> GetAllAsync(Expression<Func<TEntity, bool>> expression = null)
    {
        TEntity entity = new();
        string table = entity.GetTableName();
        StringBuilder query = new();
        foreach (PropertyInfo prop in typeof(TEntity).GetProperties())
        {
            query.Append("[").Append(prop.Name).Append("]");
            if (prop != typeof(TEntity).GetProperties()[^1]) query.Append(", ");
        }
        using (IDbConnection connection = new SqlConnection(_connectionString))
        {
            if (expression != null)
            {
                StringBuilder param = new();
                StringBuilder vals = new();
                string parameters = string.Empty;
                var values = new DynamicParameters();
                var list = CheckConditions(new HashSet<Condition>(), expression.Body, "");
                foreach (var item in list)
                {
                    param.Append($"[{item.Name}] = @{item.Name} {item.Operator} ");
                    vals.Append(@$"@{item.Name} = ""{item.Value}"", ");
                    values.Add($"@{item.Name}", item.Value);
                }
                if (param.ToString().EndsWith("And ")) parameters = param.ToString().TrimEnd('d', 'n', 'A', ' ', ',');
                else if (param.ToString().EndsWith("Or ")) parameters = param.ToString().TrimEnd('r', 'O', ' ', ',');
                else parameters = param.ToString().TrimEnd(' ');
                string rawQuery = $"Select {query} From {table} Where {parameters}";
                return await connection.QueryAsync<TEntity>(rawQuery, values);
            }
            else return await connection.QueryAsync<TEntity>($"Select {query} From {table}");
        }
    }
    public async Task InsertAsync(TEntity entity)
    {
        string table = entity.GetTableName();
        Dictionary<string, object> keyValuePairs = new Dictionary<string, object>();
        foreach (PropertyInfo property in entity.GetType().GetProperties())
        {
            if (property.Name == "Id" && property.PropertyType == typeof(int))
                continue;
            else if (property.Name == "Id" && property.PropertyType == typeof(string) || property.PropertyType == typeof(Guid))
                property.SetValue(entity, Guid.NewGuid());
            if (property.GetValue(entity) != null)
                keyValuePairs.Add(property.Name, property.GetValue(entity));
        }
        using (IDbConnection connection = new SqlConnection(_connectionString))
        {
            await connection.ExecuteAsync($"INSERT INTO {table} ({string.Join(" , ", keyValuePairs.Keys)}) " +
                $"VALUES (@{string.Join(",@", keyValuePairs.Keys)})", entity);
        }
    }
    public async Task BulkInsertAsync(IEnumerable<TEntity> entities)
    {
        string table = entities.GetTableName().TrimEnd(']').TrimEnd('[');
        Dictionary<string, object> keyValuePairs = new Dictionary<string, object>();
        foreach (var entity in entities)
        {
            foreach (var property in entity.GetType().GetProperties())
            {
                if (property.Name == "Id" && property.PropertyType == typeof(int))
                    continue;
                else if (property.Name == "Id" && property.PropertyType == typeof(string) || property.PropertyType == typeof(Guid))
                    property.SetValue(entities, Guid.NewGuid());

                if (!keyValuePairs.ContainsKey(property.Name))
                    keyValuePairs.Add(property.Name, property.GetValue(entity));
            }
        }
        using (IDbConnection connection = new SqlConnection(_connectionString))
        {
            string result = keyValuePairs.Count > 0 ? string.Join(" , ", keyValuePairs.Keys) : string.Join(" ", keyValuePairs.Keys);
            string a = $"INSERT INTO {table} ({result}) " +
                $"VALUES (@{string.Join(",@", keyValuePairs.Keys)})";
            await connection.ExecuteAsync($"INSERT INTO {table} ({result}) " +
                $"VALUES (@{string.Join(",@", keyValuePairs.Keys)})", entities);
        }
    }
    public async Task<bool> UpdateAsync(TEntity entity)
    {
        string table = entity.GetTableName();
        StringBuilder query = new();
        var values = new DynamicParameters();
        foreach (var prop in entity.GetType().GetProperties())
        {
            if (prop.GetValue(entity) != null)
            {
                if (prop.Name != "Id")
                    query.Append($"{prop.Name} = @{prop.Name}, ");
                values.Add($"@{prop.Name}", prop.GetValue(entity));
            }
        }
        using (IDbConnection connection = new SqlConnection(_connectionString))
        {
            var rawQuery = $"UPDATE {table} SET {query.ToString().TrimEnd(' ').TrimEnd(',')} WHERE Id = @Id";
            bool result = await connection.ExecuteAsync(rawQuery, values) > 0;
            return result;
        }
    }
    static HashSet<Condition> CheckConditions(HashSet<Condition> conditions, Expression expression, string nodeType)
    {
        if (expression is BinaryExpression binaryExpression)
        {
            if (expression.NodeType == ExpressionType.AndAlso || expression.NodeType == ExpressionType.OrElse)
            {
                nodeType = expression.NodeType.ToString().Replace("AndAlso", "And").Replace("OrElse", "Or");
                CheckConditions(conditions, binaryExpression.Left as BinaryExpression, nodeType);
                CheckConditions(conditions, binaryExpression.Right as BinaryExpression, nodeType);
            }
            else conditions.Add(GetCondition(binaryExpression, nodeType));
        }
        return conditions;
    }
    static Condition GetCondition(BinaryExpression binaryExpression, string nodeType)
    {
        var condition = new Condition();
        condition.Operator = nodeType;
        if (binaryExpression.Left is MemberExpression left)
        {
            condition.Name = left.Member.Name;
        }
        if (binaryExpression.Right is Expression right)
        {
            var lambdaExpression = Expression.Lambda(right);
            var value = lambdaExpression.Compile();
            condition.Value = value.DynamicInvoke();
        }
        return condition;
    }

}
