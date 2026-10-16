using System;
using System.IO;
using System.Text;
using ProjectZ.Switch.Update;

internal static class Program
{
    private static int _fail;

    private static void Check(string what, string got, string want)
    {
        bool ok = string.Equals(got, want, StringComparison.Ordinal);
        if (!ok) _fail++;
        Console.WriteLine((ok ? "OK   " : "FALLO") + "  " + what);
        if (!ok)
        {
            Console.WriteLine("        got  " + got);
            Console.WriteLine("        want " + want);
        }
    }

    private static int Main()
    {
        // Vectores conocidos de FIPS 180-4 / NIST.
        Check("vacio", SwitchSha256.HashBytes(Array.Empty<byte>()),
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
        Check("abc", SwitchSha256.HashBytes(Encoding.ASCII.GetBytes("abc")),
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
        Check("448 bits", SwitchSha256.HashBytes(Encoding.ASCII.GetBytes(
                "abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq")),
            "248d6a61d20638b8e5c026930c3e6039a33ce45964ff2167f6ecedd419db06c1");
        Check("896 bits", SwitchSha256.HashBytes(Encoding.ASCII.GetBytes(
                "abcdefghbcdefghicdefghijdefghijkefghijklfghijklmghijklmnhijklmnoijklmnopjklmnopqklmnopqrlmnopqrsmnopqrstnopqrstu")),
            "cf5b16a778af8380036ce59e7b0492370b249b11e8f07a51afac45037afee9d1");
        var million = new byte[1000000];
        for (int i = 0; i < million.Length; i++) million[i] = (byte)'a';
        Check("un millon de 'a'", SwitchSha256.HashBytes(million),
            "cdc76e5c9914fb9281a1c7e284d73e67f1809a48a497200e046d39ccc7112cd0");

        // Longitudes alrededor de los bordes del bloque y del padding, contra la
        // implementacion de la BCL (que en PC si esta disponible).
        var rnd = new Random(1234);
        foreach (int n in new[] { 1, 54, 55, 56, 57, 63, 64, 65, 119, 120, 121, 127, 128, 129, 1000, 65536, 70000 })
        {
            var buf = new byte[n];
            rnd.NextBytes(buf);
            string want = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(buf)).ToLowerInvariant();
            Check("longitud " + n, SwitchSha256.HashBytes(buf), want);
        }

        // HashFile por trozos, con un fichero mas grande que el buffer de 64 KB.
        string tmp = Path.Combine(Path.GetTempPath(), "sha256_harness.bin");
        var big = new byte[64 * 1024 * 3 + 777];
        rnd.NextBytes(big);
        File.WriteAllBytes(tmp, big);
        long size;
        string fileHash = SwitchSha256.HashFile(tmp, out size);
        Check("HashFile", fileHash,
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(big)).ToLowerInvariant());
        Check("HashFile tamano", size.ToString(), big.Length.ToString());
        File.Delete(tmp);

        // Un fichero que no existe devuelve null, no excepcion.
        Check("fichero ausente", SwitchSha256.HashFile(Path.Combine(Path.GetTempPath(), "no_existe_jamas.bin"), out size) ?? "null", "null");

        _fail += ManifestTests.Run();

        // El manifiesto DE VERDAD que se va a publicar, contra el NRO de verdad.
        Console.WriteLine("\n--- manifiesto publicado ---");
        // Carpeta del repo publico. Se puede cambiar con la variable LADXHD_RELEASE_DIR.
        string releaseDir = Environment.GetEnvironmentVariable("LADXHD_RELEASE_DIR")
            ?? @"D:\XboxGames\Ports\Links Awakening DX HD\_switchbuild\release";
        string RealManifest = Path.Combine(releaseDir, "update_manifest.json");
        string RealNro = Path.Combine(releaseDir, "ProjectZ.Switch.nro");
        if (File.Exists(RealManifest))
        {
            var real = SwitchUpdateManifest.Parse(File.ReadAllText(RealManifest));
            Check("el parser lo acepta", real != null ? "si" : "no", "si");
            if (real != null && File.Exists(RealNro))
            {
                long nroSize;
                string nroHash = SwitchSha256.HashFile(RealNro, out nroSize);
                Check("tamano declarado = NRO real", real.NroSize.ToString(), nroSize.ToString());
                Check("sha declarado = NRO real", real.NroSha256, nroHash);
                Check("la url lleva el tag de la version",
                    real.NroUrl.Contains("/v" + real.Version + "/") ? "si" : "no", "si");
                Check("version del manifiesto = PortVersion compilada", real.Version, "1.2.0");
            }
        }
        else
        {
            Console.WriteLine("(no hay manifiesto publicado todavia)");
        }

        Console.WriteLine(_fail == 0 ? "\nTODO OK" : "\n" + _fail + " FALLOS");
        return _fail == 0 ? 0 : 1;
    }
}
