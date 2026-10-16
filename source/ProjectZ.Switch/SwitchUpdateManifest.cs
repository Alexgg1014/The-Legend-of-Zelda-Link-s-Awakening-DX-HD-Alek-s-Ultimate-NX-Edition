#if SWITCH
using System;
using System.Collections.Generic;

namespace ProjectZ.Switch.Update
{
    /// <summary>
    /// El manifiesto de actualización: metadatos nuestros con un esquema CERRADO, no un scrapeo
    /// de la API de GitHub. Fija tamaño y SHA-256 esperados ANTES de bajar un solo byte del NRO.
    ///
    /// El parser es deliberadamente diminuto y estricto (calco del de Minish Cap,
    /// <c>port_update_common.c</c>): nada de System.Text.Json, que en NativeAOT arrastra
    /// reflexión y avisos de recorte. Una clave desconocida, duplicada o un sobrante tras el
    /// objeto = manifiesto rechazado.
    ///
    /// EXCEPCIÓN APRENDIDA A GOLPES (Minish Cap v1.3.2→v1.3.3): el changelog NO puede tumbar el
    /// parser. Allí el parser fallaba al pasarse del tope de líneas, así que el día que una
    /// release llevó una línea de más NADIE pudo actualizar — y tampoco se les podía mandar el
    /// arreglo, porque lo que entrega los arreglos era justo lo roto. Aquí, pasado el tope, se
    /// DEJA DE GUARDAR y se sigue. Un manifiesto son notas de versión: jamás puede ladrillar
    /// el updater.
    /// </summary>
    internal sealed class SwitchUpdateManifest
    {
        public const int MaxBytes = 16 * 1024;
        public const int MaxChangelog = 8;
        public const int MaxChangelogLine = 160;
        public const uint Schema = 1;
        public const uint UpdaterSchema = 1;
        public const long MinNroSize = 4096;
        public const long MaxNroSize = 128L * 1024 * 1024;

        public string Version;
        public string Channel;
        public string NroUrl;
        public string NroSha256;
        public long NroSize;
        public string[] Changelog = Array.Empty<string>();

        // ------------------------------------------------------------------ versiones

        /// <summary>"v1.2.0" o "1.2.0" → [1,2,0]. Devuelve false si no es un semver de 3 partes.</summary>
        public static bool TryParseVersion(string version, int[] parts)
        {
            if (version == null || parts == null || parts.Length < 3)
                return false;
            int i = 0;
            if (i < version.Length && (version[i] == 'v' || version[i] == 'V'))
                i++;
            for (int p = 0; p < 3; p++)
            {
                if (i >= version.Length || version[i] < '0' || version[i] > '9')
                    return false;
                int value = 0;
                while (i < version.Length && version[i] >= '0' && version[i] <= '9')
                {
                    if (value > 100000) return false;       // una versión sensata, no un desbordamiento
                    value = value * 10 + (version[i] - '0');
                    i++;
                }
                parts[p] = value;
                if (p < 2)
                {
                    if (i >= version.Length || version[i] != '.') return false;
                    i++;
                }
            }
            return i == version.Length;                      // nada de sufijos: "1.2.0-beta" se rechaza
        }

        /// <summary>
        /// ¿<paramref name="candidate"/> es posterior a <paramref name="installed"/>? Comparación
        /// numérica: "0.10.0" gana a "0.9.0", cosa que una comparación de cadenas hace al revés.
        /// Una versión mal formada NUNCA anuncia actualización.
        /// </summary>
        public static bool IsNewer(string candidate, string installed)
        {
            var a = new int[3];
            var b = new int[3];
            if (!TryParseVersion(candidate, a) || !TryParseVersion(installed, b))
                return false;
            for (int i = 0; i < 3; i++)
            {
                if (a[i] != b[i]) return a[i] > b[i];
            }
            return false;
        }

        // ------------------------------------------------------------------ URLs

        /// <summary>
        /// HTTPS obligatorio y host de GitHub. El manifiesto es nuestro, pero viaja por la red:
        /// esto impide que una versión manipulada mande la descarga a cualquier otro sitio.
        /// </summary>
        public static bool IsAllowedUrl(string url)
        {
            const string Prefix = "https://";
            if (url == null || url.Length <= Prefix.Length || !url.StartsWith(Prefix, StringComparison.Ordinal))
                return false;
            int start = Prefix.Length;
            int end = url.IndexOf('/', start);
            string host = end < 0 ? url.Substring(start) : url.Substring(start, end - start);
            if (host.Length == 0 || host.IndexOf('@') >= 0 || host.IndexOf(':') >= 0)
                return false;                                 // ni credenciales ni puerto
            return host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
                   host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase);
        }

        // ------------------------------------------------------------------ parser

        private int _pos;
        private string _s;

        private void SkipWs()
        {
            while (_pos < _s.Length && (_s[_pos] == ' ' || _s[_pos] == '\t' || _s[_pos] == '\r' || _s[_pos] == '\n'))
                _pos++;
        }

        private bool ReadString(int maxLen, out string value)
        {
            value = null;
            SkipWs();
            if (_pos >= _s.Length || _s[_pos] != '"')
                return false;
            _pos++;
            var sb = new System.Text.StringBuilder();
            while (_pos < _s.Length && _s[_pos] != '"')
            {
                char c = _s[_pos++];
                if (c == '\\')
                {
                    if (_pos >= _s.Length) return false;
                    char e = _s[_pos++];
                    switch (e)
                    {
                        case '"': c = '"'; break;
                        case '\\': c = '\\'; break;
                        case '/': c = '/'; break;
                        case 'b': c = '\b'; break;
                        case 'f': c = '\f'; break;
                        case 'n': c = '\n'; break;
                        case 'r': c = '\r'; break;
                        case 't': c = '\t'; break;
                        default: return false;                // \u incluido: el manifiesto es ASCII/UTF-8 literal
                    }
                }
                else if (c < 0x20)
                {
                    return false;
                }
                if (sb.Length >= maxLen) return false;
                sb.Append(c);
            }
            if (_pos >= _s.Length) return false;
            _pos++;
            value = sb.ToString();
            return true;
        }

        private bool ReadUInt(out long value)
        {
            value = 0;
            SkipWs();
            int digits = 0;
            while (_pos < _s.Length && _s[_pos] >= '0' && _s[_pos] <= '9')
            {
                if (value > (long.MaxValue - 9) / 10) return false;
                value = value * 10 + (_s[_pos] - '0');
                _pos++;
                digits++;
            }
            return digits > 0;
        }

        private bool ReadChangelog(List<string> into)
        {
            SkipWs();
            if (_pos >= _s.Length || _s[_pos] != '[')
                return false;
            _pos++;
            SkipWs();
            if (_pos < _s.Length && _s[_pos] == ']') { _pos++; return true; }
            while (true)
            {
                string line;
                if (!ReadString(MaxChangelogLine, out line))
                    return false;
                // Pasado el tope se sigue leyendo pero se deja de guardar: ver la nota de arriba.
                if (into.Count < MaxChangelog)
                    into.Add(line);
                SkipWs();
                if (_pos < _s.Length && _s[_pos] == ',') { _pos++; continue; }
                if (_pos < _s.Length && _s[_pos] == ']') { _pos++; return true; }
                return false;
            }
        }

        /// <summary>Parsea y valida. Devuelve null si el manifiesto no es exactamente lo esperado.</summary>
        public static SwitchUpdateManifest Parse(string json)
        {
            if (json == null || json.Length == 0 || json.Length > MaxBytes)
                return null;

            var m = new SwitchUpdateManifest { _s = json, _pos = 0 };
            var changelog = new List<string>();
            long schema = -1, minUpdater = -1;
            int seen = 0;

            m.SkipWs();
            if (m._pos >= json.Length || json[m._pos++] != '{')
                return null;
            m.SkipWs();
            while (m._pos < json.Length && json[m._pos] != '}')
            {
                string key;
                if (!m.ReadString(32, out key))
                    return null;
                m.SkipWs();
                if (m._pos >= json.Length || json[m._pos++] != ':')
                    return null;

                int bit;
                switch (key)
                {
                    case "schema":
                        bit = 1;
                        if (!m.ReadUInt(out schema)) return null;
                        break;
                    case "version":
                        bit = 2;
                        if (!m.ReadString(32, out m.Version)) return null;
                        break;
                    case "channel":
                        bit = 4;
                        if (!m.ReadString(12, out m.Channel)) return null;
                        break;
                    case "nro_url":
                        bit = 8;
                        if (!m.ReadString(512, out m.NroUrl)) return null;
                        break;
                    case "nro_sha256":
                        bit = 16;
                        if (!m.ReadString(65, out m.NroSha256)) return null;
                        break;
                    case "nro_size":
                        bit = 32;
                        if (!m.ReadUInt(out m.NroSize)) return null;
                        break;
                    case "min_updater_schema":
                        bit = 64;
                        if (!m.ReadUInt(out minUpdater)) return null;
                        break;
                    case "changelog":
                        bit = 128;
                        if (!m.ReadChangelog(changelog)) return null;
                        break;
                    default:
                        return null;                          // esquema cerrado a propósito
                }
                if ((seen & bit) != 0) return null;           // clave repetida
                seen |= bit;

                m.SkipWs();
                if (m._pos < json.Length && json[m._pos] == ',') { m._pos++; m.SkipWs(); continue; }
                if (m._pos >= json.Length || json[m._pos] != '}') return null;
            }
            if (m._pos >= json.Length || json[m._pos++] != '}')
                return null;
            m.SkipWs();
            if (m._pos != json.Length)
                return null;                                  // basura tras el objeto

            if (seen != 255) return null;
            if (schema != Schema || minUpdater < 0 || minUpdater > UpdaterSchema) return null;
            if (!string.Equals(m.Channel, "stable", StringComparison.Ordinal)) return null;
            if (!IsAllowedUrl(m.NroUrl)) return null;
            if (m.NroSize < MinNroSize || m.NroSize > MaxNroSize) return null;
            if (!TryParseVersion(m.Version, new int[3])) return null;
            if (m.NroSha256 == null || m.NroSha256.Length != 64) return null;

            var hex = m.NroSha256.ToCharArray();
            for (int i = 0; i < 64; i++)
            {
                char c = hex[i];
                if (c >= '0' && c <= '9') continue;
                if (c >= 'a' && c <= 'f') continue;
                if (c >= 'A' && c <= 'F') { hex[i] = (char)(c + 32); continue; }
                return null;
            }
            m.NroSha256 = new string(hex);

            if (changelog.Count == 0) return null;
            m.Changelog = changelog.ToArray();
            m._s = null;
            return m;
        }
    }
}
#endif
