using System;
using System.IO;

namespace ProjectZ.Switch.Install
{
    /// <summary>
    /// Decoder VCDIFF (RFC 3284) autocontenido, para aplicar en consola los parches de
    /// bighead sobre la copia v1.0.0 del usuario.
    ///
    /// El patcher oficial usa el paquete NuGet "VCDiff" (port de open-vcdiff) con
    /// <c>Encode(interleaved: true)</c>, así que los .vcdiff llevan la cabecera extendida de
    /// open-vcdiff (byte de versión 'S') y las tres secciones de cada ventana (datos,
    /// instrucciones, direcciones) van entrelazadas en una sola. Este decoder entiende ese
    /// formato y también el estándar de tres secciones. No soporta tablas de códigos
    /// personalizadas ni compresión secundaria (el patcher no las usa).
    ///
    /// Validado en PC contra los 570 ficheros de v2.0.8 generados por el patcher oficial (el formato
    /// no ha cambiado en v2.0.9: los parches solo difieren en los .lng).
    /// </summary>
    public static class VcdiffDecoder
    {
        private const byte VcdSource = 0x01;
        private const byte VcdTarget = 0x02;
        private const byte VcdChecksum = 0x04;   // extensión open-vcdiff
        private const byte VcdDecompress = 0x01;
        private const byte VcdCodeTable = 0x02;
        private const byte VcdAppHeader = 0x04;

        private const int NearSize = 4;
        private const int SameSize = 3;

        private enum Op : byte { NoOp = 0, Add = 1, Run = 2, Copy = 3 }

        private struct Instr
        {
            public Op Type;
            public byte Size;
            public byte Mode;
        }

        // Tabla de códigos por defecto (RFC 3284 §5.6): 256 entradas, cada una con hasta dos
        // instrucciones. Se construye una vez.
        private static readonly Instr[] Table1 = new Instr[256];
        private static readonly Instr[] Table2 = new Instr[256];

        static VcdiffDecoder()
        {
            int i = 0;
            Table1[i++] = new Instr { Type = Op.Run, Size = 0, Mode = 0 };
            Table1[i++] = new Instr { Type = Op.Add, Size = 0, Mode = 0 };
            for (byte s = 1; s <= 17; s++)
                Table1[i++] = new Instr { Type = Op.Add, Size = s, Mode = 0 };
            for (byte m = 0; m < 9; m++)
            {
                Table1[i++] = new Instr { Type = Op.Copy, Size = 0, Mode = m };
                for (byte s = 4; s <= 18; s++)
                    Table1[i++] = new Instr { Type = Op.Copy, Size = s, Mode = m };
            }
            for (byte m = 0; m < 6; m++)
                for (byte a = 1; a <= 4; a++)
                    for (byte c = 4; c <= 6; c++)
                    {
                        Table1[i] = new Instr { Type = Op.Add, Size = a, Mode = 0 };
                        Table2[i++] = new Instr { Type = Op.Copy, Size = c, Mode = m };
                    }
            for (byte m = 6; m < 9; m++)
                for (byte a = 1; a <= 4; a++)
                {
                    Table1[i] = new Instr { Type = Op.Add, Size = a, Mode = 0 };
                    Table2[i++] = new Instr { Type = Op.Copy, Size = 4, Mode = m };
                }
            for (byte m = 0; m < 9; m++)
            {
                Table1[i] = new Instr { Type = Op.Copy, Size = 4, Mode = m };
                Table2[i++] = new Instr { Type = Op.Add, Size = 1, Mode = 0 };
            }
            if (i != 256)
                throw new InvalidOperationException("tabla VCDIFF mal construida: " + i);
        }

        /// <summary>Lector secuencial sobre un tramo de byte[].</summary>
        private struct Reader
        {
            public byte[] Buf;
            public int Pos;
            public int End;

            public Reader(byte[] buf, int pos, int len) { Buf = buf; Pos = pos; End = pos + len; }

            public bool AtEnd => Pos >= End;

            public byte Byte()
            {
                if (Pos >= End) throw new InvalidDataException("VCDIFF: lectura fuera de sección");
                return Buf[Pos++];
            }

            // Entero base-128 big-endian: bit 7 = continúa (RFC 3284 §2).
            public int Varint()
            {
                int v = 0;
                for (int n = 0; n < 5; n++)
                {
                    byte b = Byte();
                    v = (v << 7) | (b & 0x7F);
                    if ((b & 0x80) == 0) return v;
                }
                throw new InvalidDataException("VCDIFF: varint demasiado largo");
            }

            public void Skip(int n)
            {
                if (Pos + n > End) throw new InvalidDataException("VCDIFF: salto fuera de sección");
                Pos += n;
            }
        }

        /// <summary>Aplica <paramref name="delta"/> sobre <paramref name="source"/> y devuelve el fichero resultante.</summary>
        public static byte[] Apply(byte[] source, byte[] delta)
        {
            var d = new Reader(delta, 0, delta.Length);

            if (delta.Length < 4 || d.Byte() != 0xD6 || d.Byte() != 0xC3 || d.Byte() != 0xC4)
                throw new InvalidDataException("VCDIFF: cabecera inválida");
            byte version = d.Byte();
            if (version != 0x00 && version != (byte)'S')
                throw new InvalidDataException("VCDIFF: versión no soportada 0x" + version.ToString("X2"));

            byte hdr = d.Byte();
            if ((hdr & VcdDecompress) != 0)
                throw new InvalidDataException("VCDIFF: compresión secundaria no soportada");
            if ((hdr & VcdCodeTable) != 0)
                throw new InvalidDataException("VCDIFF: tabla de códigos personalizada no soportada");
            if ((hdr & VcdAppHeader) != 0)
                d.Skip(d.Varint());

            // El fichero de salida crece ventana a ventana. Las ventanas VCD_TARGET pueden
            // referirse a lo ya escrito, así que se mantiene entero en memoria (los assets
            // del juego son de unos pocos MB como mucho).
            var output = new MemoryStream(source.Length + source.Length / 4 + 64);
            var near = new int[NearSize];
            var same = new int[SameSize * 256];

            while (!d.AtEnd)
                DecodeWindow(ref d, source, output, near, same);

            return output.ToArray();
        }

        private static void DecodeWindow(ref Reader d, byte[] source, MemoryStream output, int[] near, int[] same)
        {
            byte winIndicator = d.Byte();
            int srcLen = 0, srcPos = 0;
            bool hasSource = (winIndicator & (VcdSource | VcdTarget)) != 0;
            if (hasSource)
            {
                srcLen = d.Varint();
                srcPos = d.Varint();
            }

            int deltaLen = d.Varint();
            int windowStart = d.Pos;
            int targetLen = d.Varint();
            byte deltaIndicator = d.Byte();
            if (deltaIndicator != 0)
                throw new InvalidDataException("VCDIFF: secciones comprimidas no soportadas");
            int dataLen = d.Varint();
            int instrLen = d.Varint();
            int addrLen = d.Varint();

            uint expectedAdler = 0;
            bool hasChecksum = (winIndicator & VcdChecksum) != 0;
            if (hasChecksum)
                expectedAdler = (uint)d.Varint();

            // Segmento fuente de esta ventana.
            byte[] segBuf;
            int segOff;
            if ((winIndicator & VcdSource) != 0)
            {
                if (srcPos < 0 || srcLen < 0 || srcPos + srcLen > source.Length)
                    throw new InvalidDataException("VCDIFF: segmento fuente fuera del original (¿fichero de otra versión?)");
                segBuf = source;
                segOff = srcPos;
            }
            else if ((winIndicator & VcdTarget) != 0)
            {
                var written = output.GetBuffer();
                if (srcPos + srcLen > output.Length)
                    throw new InvalidDataException("VCDIFF: segmento VCD_TARGET fuera de lo ya decodificado");
                // Copia: el MemoryStream puede realojar su buffer mientras escribimos.
                segBuf = new byte[srcLen];
                Buffer.BlockCopy(written, srcPos, segBuf, 0, srcLen);
                segOff = 0;
            }
            else
            {
                segBuf = Array.Empty<byte>();
                segOff = 0;
            }

            var target = new byte[targetLen];
            int tpos = 0;

            // Interleaved (open-vcdiff): datos y direcciones van dentro de la sección de
            // instrucciones, y las otras dos secciones miden 0.
            bool interleaved = dataLen == 0 && addrLen == 0;
            var data = new Reader(d.Buf, d.Pos, dataLen);
            var instr = new Reader(d.Buf, d.Pos + dataLen, instrLen);
            var addr = new Reader(d.Buf, d.Pos + dataLen + instrLen, addrLen);

            // Caché de direcciones: se reinicia en cada ventana (RFC 3284 §5.1).
            Array.Clear(near, 0, near.Length);
            Array.Clear(same, 0, same.Length);
            int nextNear = 0;

            while (tpos < targetLen)
            {
                byte code = instr.Byte();
                var i1 = Table1[code];
                var i2 = Table2[code];
                Execute(i1, ref instr, ref data, ref addr, interleaved, segBuf, segOff, srcLen, target, ref tpos, near, same, ref nextNear);
                if (i2.Type != Op.NoOp)
                    Execute(i2, ref instr, ref data, ref addr, interleaved, segBuf, segOff, srcLen, target, ref tpos, near, same, ref nextNear);
            }
            if (tpos != targetLen)
                throw new InvalidDataException("VCDIFF: la ventana produjo " + tpos + " bytes, esperados " + targetLen);

            if (hasChecksum)
            {
                uint adler = Adler32(target);
                if (adler != expectedAdler)
                    throw new InvalidDataException("VCDIFF: checksum de ventana incorrecto (el original no es el esperado)");
            }

            output.Write(target, 0, targetLen);

            // Saltar al final de la ventana, calculado desde la cabecera y no desde lo leído.
            d.Pos = windowStart + deltaLen;
            if (d.Pos > d.End)
                throw new InvalidDataException("VCDIFF: la ventana declara más bytes de los que hay");
        }

        private static void Execute(Instr ins, ref Reader instr, ref Reader data, ref Reader addr, bool interleaved,
            byte[] segBuf, int segOff, int srcLen, byte[] target, ref int tpos, int[] near, int[] same, ref int nextNear)
        {
            int size = ins.Size;
            if (size == 0)
                size = instr.Varint();
            if (tpos + size > target.Length)
                throw new InvalidDataException("VCDIFF: instrucción desborda la ventana");

            switch (ins.Type)
            {
                case Op.Add:
                    {
                        ref Reader src = ref (interleaved ? ref instr : ref data);
                        for (int k = 0; k < size; k++)
                            target[tpos + k] = src.Byte();
                        tpos += size;
                        break;
                    }
                case Op.Run:
                    {
                        ref Reader src = ref (interleaved ? ref instr : ref data);
                        byte b = src.Byte();
                        for (int k = 0; k < size; k++)
                            target[tpos + k] = b;
                        tpos += size;
                        break;
                    }
                case Op.Copy:
                    {
                        ref Reader src = ref (interleaved ? ref instr : ref addr);
                        int here = srcLen + tpos;
                        int a = DecodeAddress(ref src, ins.Mode, here, near, same, ref nextNear);
                        if (a < 0 || a + size > here)
                            throw new InvalidDataException("VCDIFF: dirección de copia inválida");

                        if (a + size <= srcLen)
                        {
                            Buffer.BlockCopy(segBuf, segOff + a, target, tpos, size);
                        }
                        else
                        {
                            // Puede solapar con lo que se está escribiendo (RUN largo por copia),
                            // así que byte a byte.
                            for (int k = 0; k < size; k++)
                            {
                                int p = a + k;
                                target[tpos + k] = p < srcLen ? segBuf[segOff + p] : target[p - srcLen];
                            }
                        }
                        tpos += size;
                        break;
                    }
            }
        }

        private static int DecodeAddress(ref Reader src, int mode, int here, int[] near, int[] same, ref int nextNear)
        {
            int a;
            if (mode == 0)
                a = src.Varint();                       // VCD_SELF
            else if (mode == 1)
                a = here - src.Varint();                // VCD_HERE
            else if (mode < 2 + NearSize)
                a = near[mode - 2] + src.Varint();      // VCD_NEAR
            else
                a = same[(mode - 2 - NearSize) * 256 + src.Byte()]; // VCD_SAME

            if (a < 0)
                throw new InvalidDataException("VCDIFF: dirección negativa");
            near[nextNear] = a;
            nextNear = (nextNear + 1) % NearSize;
            same[a % (SameSize * 256)] = a;
            return a;
        }

        private static uint Adler32(byte[] buf)
        {
            uint a = 1, b = 0;
            int n = buf.Length, i = 0;
            while (n > 0)
            {
                int chunk = n > 5552 ? 5552 : n;
                for (int k = 0; k < chunk; k++)
                {
                    a += buf[i++];
                    b += a;
                }
                a %= 65521;
                b %= 65521;
                n -= chunk;
            }
            return (b << 16) | a;
        }
    }
}
