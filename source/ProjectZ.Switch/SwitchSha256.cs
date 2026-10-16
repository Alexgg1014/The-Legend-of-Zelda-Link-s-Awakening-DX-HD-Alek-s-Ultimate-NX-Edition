#if SWITCH
using System;
using System.IO;

namespace ProjectZ.Switch.Update
{
    /// <summary>
    /// SHA-256 propio (FIPS 180-4), en C# puro.
    ///
    /// NO se puede usar <c>System.Security.Cryptography.SHA256</c> aquí: el csproj excluye
    /// <c>System.Security.Cryptography.Native</c> del enlace y <c>crypto_stubs.c</c> deja 275
    /// stubs <c>CryptoNative_*</c> precisamente porque el juego nunca los llamaba. Si el updater
    /// los llamara, obtendría basura en vez de un hash — y el hash es lo único que separa
    /// "descarga correcta" de "escribo cualquier cosa encima del NRO que estás ejecutando".
    ///
    /// Son 60 líneas y se validan contra los vectores conocidos del propio estándar, así que
    /// sale más barato y más seguro que tocar el enlace. Ver <c>tools/sha256_selftest.py</c>.
    /// </summary>
    internal static class SwitchSha256
    {
        private static readonly uint[] K =
        {
            0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
            0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
            0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
            0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
            0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
            0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
            0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
            0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2,
        };

        private sealed class State
        {
            public uint H0 = 0x6a09e667, H1 = 0xbb67ae85, H2 = 0x3c6ef372, H3 = 0xa54ff53a;
            public uint H4 = 0x510e527f, H5 = 0x9b05688c, H6 = 0x1f83d9ab, H7 = 0x5be0cd19;
            public readonly byte[] Block = new byte[64];
            public int BlockLen;
            public ulong TotalBytes;
            public readonly uint[] W = new uint[64];
        }

        private static uint Ror(uint x, int n) => (x >> n) | (x << (32 - n));

        private static void Compress(State s, byte[] data, int offset)
        {
            var w = s.W;
            for (int i = 0; i < 16; i++)
            {
                int o = offset + i * 4;
                w[i] = ((uint)data[o] << 24) | ((uint)data[o + 1] << 16) | ((uint)data[o + 2] << 8) | data[o + 3];
            }
            for (int i = 16; i < 64; i++)
            {
                uint s0 = Ror(w[i - 15], 7) ^ Ror(w[i - 15], 18) ^ (w[i - 15] >> 3);
                uint s1 = Ror(w[i - 2], 17) ^ Ror(w[i - 2], 19) ^ (w[i - 2] >> 10);
                w[i] = unchecked(w[i - 16] + s0 + w[i - 7] + s1);
            }

            uint a = s.H0, b = s.H1, c = s.H2, d = s.H3, e = s.H4, f = s.H5, g = s.H6, h = s.H7;
            for (int i = 0; i < 64; i++)
            {
                uint S1 = Ror(e, 6) ^ Ror(e, 11) ^ Ror(e, 25);
                uint ch = (e & f) ^ (~e & g);
                uint t1 = unchecked(h + S1 + ch + K[i] + w[i]);
                uint S0 = Ror(a, 2) ^ Ror(a, 13) ^ Ror(a, 22);
                uint maj = (a & b) ^ (a & c) ^ (b & c);
                uint t2 = unchecked(S0 + maj);
                h = g; g = f; f = e;
                e = unchecked(d + t1);
                d = c; c = b; b = a;
                a = unchecked(t1 + t2);
            }
            unchecked
            {
                s.H0 += a; s.H1 += b; s.H2 += c; s.H3 += d;
                s.H4 += e; s.H5 += f; s.H6 += g; s.H7 += h;
            }
        }

        private static void Update(State s, byte[] data, int count)
        {
            s.TotalBytes += (ulong)count;
            int pos = 0;
            if (s.BlockLen > 0)
            {
                int need = 64 - s.BlockLen;
                int take = count < need ? count : need;
                Buffer.BlockCopy(data, 0, s.Block, s.BlockLen, take);
                s.BlockLen += take;
                pos += take;
                if (s.BlockLen < 64)
                    return;
                Compress(s, s.Block, 0);
                s.BlockLen = 0;
            }
            while (count - pos >= 64)
            {
                Compress(s, data, pos);
                pos += 64;
            }
            if (count - pos > 0)
            {
                Buffer.BlockCopy(data, pos, s.Block, 0, count - pos);
                s.BlockLen = count - pos;
            }
        }

        private static string Finish(State s)
        {
            ulong bitLen = s.TotalBytes * 8UL;
            var pad = new byte[128];
            int padLen = s.BlockLen < 56 ? 56 - s.BlockLen : 120 - s.BlockLen;
            pad[0] = 0x80;
            for (int i = 0; i < 8; i++)
                pad[padLen + i] = (byte)(bitLen >> (56 - i * 8));
            Update(s, pad, padLen + 8);

            var h = new[] { s.H0, s.H1, s.H2, s.H3, s.H4, s.H5, s.H6, s.H7 };
            var chars = new char[64];
            const string Hex = "0123456789abcdef";
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    byte v = (byte)(h[i] >> (24 - j * 8));
                    chars[i * 8 + j * 2] = Hex[v >> 4];
                    chars[i * 8 + j * 2 + 1] = Hex[v & 0xF];
                }
            }
            return new string(chars);
        }

        /// <summary>Hash en hexadecimal minúsculo de un bloque en memoria.</summary>
        public static string HashBytes(byte[] data)
        {
            var s = new State();
            Update(s, data, data.Length);
            return Finish(s);
        }

        /// <summary>
        /// Hash de un fichero leyéndolo por trozos. La ruta NO lleva prefijo "sdmc:" (regla de
        /// oro del port). Devuelve null si el fichero no se puede leer entero.
        /// </summary>
        public static string HashFile(string path, out long size)
        {
            size = 0;
            try
            {
                var s = new State();
                var buf = new byte[64 * 1024];
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, buf.Length))
                {
                    int n;
                    while ((n = fs.Read(buf, 0, buf.Length)) > 0)
                        Update(s, buf, n);
                }
                size = (long)s.TotalBytes;
                return Finish(s);
            }
            catch (Exception)
            {
                size = 0;
                return null;
            }
        }
    }
}
#endif
