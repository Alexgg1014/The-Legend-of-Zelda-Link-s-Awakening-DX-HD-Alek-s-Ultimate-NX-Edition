using System;
using ProjectZ.Switch.Update;

internal static class ManifestTests
{
    private static int _fail;
    private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static void Ok(string what, bool cond)
    {
        if (!cond) _fail++;
        Console.WriteLine((cond ? "OK   " : "FALLO") + "  " + what);
    }

    private static string Good(string overrides = null)
    {
        return "{\n" +
               "  \"schema\": 1,\n" +
               "  \"version\": \"1.3.0\",\n" +
               "  \"channel\": \"stable\",\n" +
               "  \"nro_url\": \"https://github.com/Alexgg1014/repo/releases/download/v1.3.0/ProjectZ.Switch.nro\",\n" +
               "  \"nro_sha256\": \"" + Hash + "\",\n" +
               "  \"nro_size\": 53036404,\n" +
               "  \"min_updater_schema\": 1,\n" +
               "  \"changelog\": [\"una cosa\", \"otra cosa\"]\n" +
               "}";
    }

    private static void Reject(string what, string json)
    {
        Ok("rechaza " + what, SwitchUpdateManifest.Parse(json) == null);
    }

    public static int Run()
    {
        Console.WriteLine("\n--- manifiesto ---");
        var m = SwitchUpdateManifest.Parse(Good());
        Ok("acepta el manifiesto bueno", m != null);
        if (m != null)
        {
            Ok("  version", m.Version == "1.3.0");
            Ok("  size", m.NroSize == 53036404);
            Ok("  sha", m.NroSha256 == Hash);
            Ok("  changelog 2 lineas", m.Changelog.Length == 2 && m.Changelog[0] == "una cosa");
        }

        // El sha en mayusculas se normaliza a minusculas.
        var up = SwitchUpdateManifest.Parse(Good().Replace(Hash, Hash.ToUpperInvariant()));
        Ok("normaliza el sha a minusculas", up != null && up.NroSha256 == Hash);

        Reject("clave desconocida", Good().Replace("\"schema\": 1,", "\"schema\": 1, \"extra\": 1,"));
        Reject("clave duplicada", Good().Replace("\"schema\": 1,", "\"schema\": 1, \"schema\": 1,"));
        Reject("falta una clave", Good().Replace("  \"channel\": \"stable\",\n", ""));
        Reject("schema distinto de 1", Good().Replace("\"schema\": 1", "\"schema\": 2"));
        Reject("min_updater_schema mayor", Good().Replace("\"min_updater_schema\": 1", "\"min_updater_schema\": 2"));
        Reject("canal que no es stable", Good().Replace("\"stable\"", "\"beta\""));
        Reject("url http", Good().Replace("https://github.com", "http://github.com"));
        Reject("url de otro host", Good().Replace("https://github.com", "https://evil.example.com"));
        Reject("url con credenciales", Good().Replace("https://github.com", "https://a@github.com"));
        Reject("url con puerto", Good().Replace("https://github.com", "https://github.com:8443"));
        Reject("sha corto", Good().Replace(Hash, Hash.Substring(1)));
        Reject("sha no hexadecimal", Good().Replace(Hash, Hash.Substring(0, 63) + "z"));
        Reject("tamano por debajo del minimo", Good().Replace("53036404", "100"));
        Reject("tamano por encima del maximo", Good().Replace("53036404", "999999999"));
        Reject("version con sufijo", Good().Replace("\"1.3.0\"", "\"1.3.0-beta\""));
        Reject("version de 2 partes", Good().Replace("\"1.3.0\"", "\"1.3\""));
        Reject("changelog vacio", Good().Replace("[\"una cosa\", \"otra cosa\"]", "[]"));
        Reject("basura tras el objeto", Good() + "basura");
        Reject("json truncado", Good().Substring(0, Good().Length - 1));
        Reject("no es un objeto", "[1,2,3]");
        Reject("vacio", "");
        Reject("escape unicode", Good().Replace("una cosa", "una \\u0063osa"));

        // La leccion de Minish Cap: un changelog largo NO puede tumbar el parser.
        var many = new System.Text.StringBuilder("[");
        for (int i = 0; i < 25; i++) many.Append(i == 0 ? "" : ", ").Append("\"linea ").Append(i).Append("\"");
        many.Append("]");
        var big = SwitchUpdateManifest.Parse(Good().Replace("[\"una cosa\", \"otra cosa\"]", many.ToString()));
        Ok("changelog de 25 lineas se acepta", big != null);
        Ok("  y se recorta a 8", big != null && big.Changelog.Length == 8);

        Console.WriteLine("\n--- versiones ---");
        Ok("1.3.0 > 1.2.0", SwitchUpdateManifest.IsNewer("1.3.0", "1.2.0"));
        Ok("0.10.0 > 0.9.0", SwitchUpdateManifest.IsNewer("0.10.0", "0.9.0"));
        Ok("1.2.1 > 1.2.0", SwitchUpdateManifest.IsNewer("1.2.1", "1.2.0"));
        Ok("1.2.0 no > 1.2.0", !SwitchUpdateManifest.IsNewer("1.2.0", "1.2.0"));
        Ok("1.1.9 no > 1.2.0", !SwitchUpdateManifest.IsNewer("1.1.9", "1.2.0"));
        Ok("v1.3.0 con prefijo v", SwitchUpdateManifest.IsNewer("v1.3.0", "1.2.0"));
        Ok("version rota nunca anuncia update", !SwitchUpdateManifest.IsNewer("latest", "1.2.0"));
        Ok("version local rota nunca anuncia update", !SwitchUpdateManifest.IsNewer("1.3.0", "jaja"));

        return _fail;
    }
}
