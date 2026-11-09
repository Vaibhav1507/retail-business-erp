using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Npgsql;

namespace Rbos.Api.Persistence;

/// <summary>Stores ASP.NET Data Protection keys in PostgreSQL so logins and CSRF tokens survive restarts and new instances.</summary>
public sealed class PgXmlRepository(NpgsqlDataSource ds) : IXmlRepository
{
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var con = ds.OpenConnection();
        using var cmd = new NpgsqlCommand("select xml from identity.data_protection_key order by created_at", con);
        using var r = cmd.ExecuteReader();
        var list = new List<XElement>();
        while (r.Read()) list.Add(XElement.Parse(r.GetString(0)));
        return list;
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        using var con = ds.OpenConnection();
        using var cmd = new NpgsqlCommand("insert into identity.data_protection_key(name, xml) values (@n, @x) on conflict (name) do nothing", con);
        cmd.Parameters.AddWithValue("n", friendlyName);
        cmd.Parameters.AddWithValue("x", element.ToString(SaveOptions.DisableFormatting));
        cmd.ExecuteNonQuery();
    }
}
