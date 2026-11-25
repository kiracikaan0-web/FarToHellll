// FarToHell - "Hell Ambient": sakin ve urkutucu cehennem atmosferi.
// Ince/tiz ses yok: tum katmanlar alcak-orta bolgede, master 2.8 kHz alcak geciren filtreden geciyor.
// Katmanlar: derin dron, alcak koro (formant), uzak boguk can, yavas kalp atisi,
// cehennem nefesi (filtreli ruzgar), uzak inilti, koyu cello motifi. 60 sn kusursuz dongu.
using System;
using System.IO;

public static class HellAmbient
{
    const int SR = 44100;
    const double LEN = 60.0;
    static int N;
    static float[] L, R, SL, SR_;
    static Random rng = new Random(4040);
    static double send = 0.4;

    static double Mtof(double m) { return 440.0 * Math.Pow(2.0, (m - 69.0) / 12.0); }
    static double Q(double f) { return Math.Round(f * LEN) / LEN; }
    static double Coef(double fc) { return 1 - Math.Exp(-2 * Math.PI * fc / SR); }
    static double Smooth(double x) { x = Math.Max(0, Math.Min(1, x)); return x * x * (3 - 2 * x); }

    class BP
    {
        double b0, b2, a1, a2, x1, x2, y1, y2;
        public void Set(double f, double q)
        {
            double w = 2 * Math.PI * f / SR, al = Math.Sin(w) / (2 * q), a0 = 1 + al;
            b0 = al / a0; b2 = -al / a0; a1 = -2 * Math.Cos(w) / a0; a2 = (1 - al) / a0;
        }
        public double P(double x)
        {
            double y = b0 * x + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = x; y2 = y1; y1 = y;
            return y;
        }
    }

    static void Put(int i, double v, double pan)
    {
        int k = ((i % N) + N) % N;
        double a = (pan + 1.0) * Math.PI / 4.0;
        double l = v * Math.Cos(a), r = v * Math.Sin(a);
        L[k] += (float)l; R[k] += (float)r;
        SL[k] += (float)(l * send); SR_[k] += (float)(r * send);
    }

    static void PutLR(int i, double l, double r)
    {
        int k = ((i % N) + N) % N;
        L[k] += (float)l; R[k] += (float)r;
        SL[k] += (float)(l * send); SR_[k] += (float)(r * send);
    }

    // Derin dron: D1 + A1, az harmonikli, yavas nefes
    static void Drone(double amp)
    {
        send = 0.3;
        double[] fl = { Q(Mtof(26)), Q(Mtof(33)), Q(Mtof(38)) };
        double[] fr = { Q(Mtof(26.07)), Q(Mtof(33.06)), Q(Mtof(38.05)) };
        double[] w = { 1.0, 0.4, 0.3 };
        for (int i = 0; i < N; i++)
        {
            double t = (double)i / SR;
            double bright = 0.75 - 0.25 * Math.Sin(2 * Math.PI * t * 2.0 / LEN);
            double am = 0.7 + 0.3 * Math.Sin(2 * Math.PI * t * 3.0 / LEN);
            double l = 0, r = 0;
            for (int v = 0; v < 3; v++)
                for (int h = 1; h <= 6; h++)
                {
                    double g = w[v] * Math.Exp(-h * bright) / h;
                    l += g * Math.Sin(2 * Math.PI * fl[v] * h * t);
                    r += g * Math.Sin(2 * Math.PI * fr[v] * h * t);
                }
            PutLR(i, l * am * amp, r * am * amp);
        }
    }

    // Alcak koro sesi: detune testere + iki formant filtresi ("oo"/"oh")
    static void Choir(double startSec, double durSec, double midi, double amp, double pan, double f1, double f2)
    {
        Choir(startSec, durSec, midi, amp, pan, f1, f2, 2.8, 3.8);
    }

    static void Choir(double startSec, double durSec, double midi, double amp, double pan, double f1, double f2, double att, double rel)
    {
        send = 0.7;
        double total = durSec + rel;
        int s0 = (int)(startSec * SR), n = (int)(total * SR);
        double f = Mtof(midi), pa = rng.NextDouble(), pb = rng.NextDouble(), vph = rng.NextDouble() * 6.28;
        BP fa = new BP(), fb = new BP(); fa.Set(f1, 4.5); fb.Set(f2, 6.0);
        double lp = 0, aLp = Coef(500);
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / SR;
            double vib = 0.08 * Math.Sin(2 * Math.PI * 4.3 * t + vph);
            double ff = f * Math.Pow(2, vib / 12.0);
            pa += ff * 1.003 / SR; if (pa >= 1) pa -= 1;
            pb += ff * 0.997 / SR; if (pb >= 1) pb -= 1;
            double x = (2 * pa - 1) + (2 * pb - 1);
            lp += aLp * (x - lp);
            double v = fa.P(x) * 1.0 + fb.P(x) * 0.45 + lp * 0.25;
            double e = t < att ? Smooth(t / att) : (t < durSec ? 1.0 : 1 - Smooth((t - durSec) / rel));
            Put(s0 + i, v * e * amp, pan);
        }
    }

    // Uzak, boguk can: tum kismilar ~300 Hz altinda
    static void Bell(double startSec, double baseMidi, double amp)
    {
        send = 0.8;
        double[] ratio = { 1, 1.51, 2.23, 2.74, 3.61 };
        double[] am = { 1, .55, .4, .25, .15 };
        double[] dec = { 0.28, 0.4, 0.55, 0.75, 1.0 };
        double f0 = Mtof(baseMidi), total = 12.0;
        int s0 = (int)(startSec * SR), n = (int)(total * SR);
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / SR, l = 0, r = 0;
            for (int p = 0; p < ratio.Length; p++)
            {
                double f = f0 * ratio[p], g = am[p] * Math.Exp(-t * dec[p]);
                l += g * Math.Sin(2 * Math.PI * (f - 0.2) * t);
                r += g * Math.Sin(2 * Math.PI * (f + 0.2) * t);
            }
            double e = Smooth(t / 0.05) * Math.Min((total - t) / 1.0, 1);
            PutLR(s0 + i, l * e * amp, r * e * amp);
        }
    }

    // Yavas, yumusak kalp atisi
    static void Heart(double startSec, double amp)
    {
        send = 0.25;
        int s0 = (int)(startSec * SR), n = (int)(0.7 * SR);
        double ph = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / SR;
            ph += 2 * Math.PI * (30 + 22 * Math.Exp(-t * 18)) / SR;
            double v = Math.Sin(ph) * Math.Exp(-t * 6) * Smooth(t / 0.012);
            Put(s0 + i, v * amp, 0);
        }
    }

    // Cehennem nefesi: dolasan bant geciren filtreli gurultu (150-450 Hz)
    static void Breath(double amp)
    {
        send = 0.45;
        BP bl = new BP(), br = new BP();
        double[] ol = new double[N], or_ = new double[N];
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < N; i++)
            {
                double t = (double)i / SR;
                if ((i & 31) == 0)
                {
                    double sweep = 0.5 + 0.5 * Math.Sin(2 * Math.PI * t * 5.0 / LEN);
                    bl.Set(150 + 300 * sweep, 1.6);
                    br.Set(160 + 290 * (0.5 + 0.5 * Math.Sin(2 * Math.PI * t * 5.0 / LEN + 0.8)), 1.6);
                }
                double xl = bl.P(rng.NextDouble() * 2 - 1), xr = br.P(rng.NextDouble() * 2 - 1);
                if (pass == 1) { ol[i] = xl; or_[i] = xr; }
            }
        double sum = 0;
        for (int i = 0; i < N; i++) sum += ol[i] * ol[i];
        double g = amp / Math.Sqrt(sum / N);
        for (int i = 0; i < N; i++)
        {
            double t = (double)i / SR;
            // yavas nefes al-ver (6 sn periyot)
            double sw = 0.35 + 0.65 * Math.Pow(0.5 + 0.5 * Math.Sin(2 * Math.PI * t * 10.0 / LEN), 2);
            PutLR(i, ol[i] * g * sw, or_[i] * g * sw);
        }
    }

    // Uzak inilti: asagi kayan, formantli alcak ses
    static void Moan(double startSec, double fromMidi, double toMidi, double durSec, double amp, double pan)
    {
        send = 0.85;
        int s0 = (int)(startSec * SR), n = (int)((durSec + 1.5) * SR);
        BP fa = new BP(), fb = new BP(); fa.Set(350, 4); fb.Set(750, 5);
        double ph = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / SR;
            double x = Math.Min(t / durSec, 1);
            double m = fromMidi + (toMidi - fromMidi) * Smooth(x) + 0.12 * Math.Sin(2 * Math.PI * 3.1 * t);
            ph += Mtof(m) / SR; if (ph >= 1) ph -= 1;
            double s = 2 * ph - 1;
            double v = fa.P(s) + 0.4 * fb.P(s);
            double e = Smooth(t / 1.2) * (t < durSec ? 1 : 1 - Smooth((t - durSec) / 1.5));
            Put(s0 + i, v * e * amp, pan);
        }
    }

    // Koyu cello: yumusak atak, dar filtre
    static void Cello(double startSec, double midi, double durSec, double amp, double pan)
    {
        send = 0.6;
        double att = 0.6, rel = 1.5, total = durSec + rel;
        int s0 = (int)(startSec * SR), n = (int)(total * SR);
        double f = Mtof(midi), pa = rng.NextDouble(), pb = rng.NextDouble(), y1 = 0, y2 = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / SR;
            double vib = 0.12 * Math.Sin(2 * Math.PI * 4.8 * t) * Smooth((t - 0.4) / 0.8);
            double ff = f * Math.Pow(2, vib / 12.0);
            pa += ff * 1.003 / SR; if (pa >= 1) pa -= 1;
            pb += ff * 0.997 / SR; if (pb >= 1) pb -= 1;
            double x = (2 * pa - 1) + (2 * pb - 1);
            double e = t < att ? Smooth(t / att) : (t < durSec ? 1.0 : 1 - Smooth((t - durSec) / rel));
            double a = Coef(250 + 550 * e);
            y1 += a * (x - y1); y2 += a * (y1 - y2);
            Put(s0 + i, y2 * e * amp, pan);
        }
    }

    static float[] Reverb(float[] input, int spread)
    {
        int[] cd = { 1557, 1617, 1491, 1422, 1277, 1356, 1188, 1116 };
        int[] ad = { 556, 441, 341, 225 };
        double fb = 0.92, damp = 0.5, g = 0.5;
        float[][] cb = new float[cd.Length][]; int[] ci = new int[cd.Length]; double[] cf = new double[cd.Length];
        for (int c = 0; c < cd.Length; c++) cb[c] = new float[cd[c] + spread];
        float[][] ab = new float[ad.Length][]; int[] ai = new int[ad.Length];
        for (int c = 0; c < ad.Length; c++) ab[c] = new float[ad[c] + spread];
        float[] o = new float[N];
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < N; i++)
            {
                double x = input[i] * 0.1, s = 0;
                for (int c = 0; c < cd.Length; c++)
                {
                    double y = cb[c][ci[c]];
                    cf[c] = y * (1 - damp) + cf[c] * damp;
                    cb[c][ci[c]] = (float)(x + cf[c] * fb);
                    if (++ci[c] >= cb[c].Length) ci[c] = 0;
                    s += y;
                }
                for (int c = 0; c < ad.Length; c++)
                {
                    double bo = ab[c][ai[c]];
                    double ov = -s + bo;
                    ab[c][ai[c]] = (float)(s + bo * g);
                    if (++ai[c] >= ab[c].Length) ai[c] = 0;
                    s = ov;
                }
                if (pass == 1) o[i] = (float)s;
            }
        return o;
    }

    // Dairesel 2 kutuplu alcak geciren (master): tiz sesleri keser
    static void LowpassCircular(double[] x, double fc)
    {
        double a = Coef(fc), y1 = 0, y2 = 0;
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < N; i++)
            {
                y1 += a * (x[i] - y1); y2 += a * (y1 - y2);
                if (pass == 1) x[i] = y2;
            }
    }

    public static void Run(string path)
    {
        N = (int)Math.Round(LEN * SR);
        L = new float[N]; R = new float[N]; SL = new float[N]; SR_ = new float[N];

        Console.WriteLine("Dron...");
        Drone(0.07);
        Console.WriteLine("Nefes...");
        Breath(0.035);

        Console.WriteLine("Koro...");
        // Dm | Bb/D | Gm/D | Eb/D (Frigyen) | Dm | A(b9)/D  -> 10'ar sn
        int[][] chords = {
            new int[]{45,50,53,57}, new int[]{46,50,53}, new int[]{43,50,55},
            new int[]{51,55,58},    new int[]{45,50,53,57}, new int[]{49,52,58}
        };
        for (int c = 0; c < chords.Length; c++)
        {
            bool oh = c % 2 == 0;
            for (int j = 0; j < chords[c].Length; j++)
            {
                double pan = (j - (chords[c].Length - 1) / 2.0) * 0.5;
                Choir(c * 10.0, 10.0, chords[c][j], 0.045, pan, oh ? 450 : 320, oh ? 800 : 870);
            }
        }

        Console.WriteLine("Canlar...");
        Bell(0, 38, 0.07);
        Bell(15, 38, 0.05);
        Bell(30, 39, 0.07);
        Bell(45, 37, 0.05);

        Console.WriteLine("Kalp...");
        for (int k = 0; k < 50; k++)
        {
            double t = k * 1.2;
            double sw = 0.6 + 0.4 * Math.Sin(2 * Math.PI * t * 2.0 / LEN);
            Heart(t, 0.22 * sw);
            Heart(t + 0.32, 0.14 * sw);
        }

        Console.WriteLine("Inilti...");
        Moan(23, 45, 38, 5.5, 0.03, -0.5);
        Moan(53, 44, 39, 5.0, 0.028, 0.5);

        Console.WriteLine("Cello...");
        double[,] motif = {
            {4,57,2.5},{6.5,58,3},{9.5,57,3.5},
            {34,50,2},{36,51,3},{39,50,4},
            {44,45,3},{47,46,4.5}
        };
        for (int k = 0; k < motif.GetLength(0); k++)
            Cello(motif[k, 0], motif[k, 1], motif[k, 2], 0.06, k % 2 == 0 ? -0.2 : 0.2);

        Console.WriteLine("Reverb...");
        float[] wl = Reverb(SL, 0), wr = Reverb(SR_, 23);

        Console.WriteLine("Mix + yazma...");
        double[] ol = new double[N], or_ = new double[N];
        for (int i = 0; i < N; i++) { ol[i] = L[i] + wl[i] * 1.1; or_[i] = R[i] + wr[i] * 1.1; }
        LowpassCircular(ol, 2800); LowpassCircular(or_, 2800);
        double peak = 0;
        for (int i = 0; i < N; i++)
        {
            ol[i] = Math.Tanh(ol[i] * 1.2); or_[i] = Math.Tanh(or_[i] * 1.2);
            peak = Math.Max(peak, Math.Max(Math.Abs(ol[i]), Math.Abs(or_[i])));
        }
        WriteWav(path, ol, or_, 0.85 / peak);
        Console.WriteLine("Tamam: " + path + "  (" + (N / (double)SR) + " sn, tepe " + peak.ToString("F3") + ")");
    }

    // ---------------- COK SESLI (POLIFONIK) VERSIYON ----------------
    // Ayni karanlik zemin (dron, nefes, can, kalp, inilti) uzerinde dort bagimsiz ses:
    // bas (cello), tenor (koro "oh"), alto (viyola benzeri cello), ust ses (koro "oo").
    // Her ses kendi ritminde hareket eder; gecikmeli girisler, gecis notalari ve
    // asili (suspension) notalar ile kontrpuan dokusu. Akor cercevesi:
    // Dm | Bb | Gm | Eb | Dm | A(b9)  -> her biri 8 vurus. 48 BPM: 1 vurus = 1.25 sn.
    const double PB = 1.25;

    // {vurus, midi, uzunluk(vurus)}
    static readonly double[,] VBass = {
        {0,50,4},{4,48,4},
        {8,46,6},{14,45,2},
        {16,43,6},{22,46,2},
        {24,39,4},{28,41,4},
        {32,38,4},{36,41,2},{38,40,2},
        {40,45,4},{44,46,2},{46,45,2}
    };
    static readonly double[,] VTenor = {
        {0,57,3},{3,58,1},{4,57,4},
        {8,58,4},{12,57,2},{14,55,2},
        {16,55,4},{20,58,4},
        {24,55,2},{26,56,2},{28,58,4},
        {32,57,4},{36,53,4},
        {40,52,4},{44,55,2},{46,53,2}
    };
    static readonly double[,] VAlto = {
        {2,62,2},{4,64,2},{6,65,2},
        {8,65,3},{11,64,1},{12,62,4},
        {16,62,4},{20,65,2},{22,63,2},
        {24,63,4},{28,62,2},{30,65,2},
        {32,65,4},{36,64,2},{38,62,2},
        {40,61,4},{44,62,2},{46,61,2}
    };
    static readonly double[,] VTop = {
        {8,70,4},{12,69,2},{14,67,2},
        {16,67,4},{20,70,4},
        {24,70,3},{27,68,1},{28,67,4},
        {32,69,6},{38,67,2},
        {40,64,4},{44,65,2},{46,64,2}
    };

    static void PlayCelloLine(double[,] line, double amp, double pan)
    {
        for (int k = 0; k < line.GetLength(0); k++)
            Cello(line[k, 0] * PB, line[k, 1], line[k, 2] * PB, amp, pan);
    }

    static void PlayChoirLine(double[,] line, double amp, double pan, double f1, double f2)
    {
        for (int k = 0; k < line.GetLength(0); k++)
        {
            double dur = line[k, 2] * PB;
            Choir(line[k, 0] * PB, dur, line[k, 1], amp, pan, f1, f2, Math.Min(0.7, dur * 0.3), 1.3);
        }
    }

    public static void RunPolyphonic(string path)
    {
        N = (int)Math.Round(LEN * SR);
        L = new float[N]; R = new float[N]; SL = new float[N]; SR_ = new float[N];

        Console.WriteLine("Zemin: dron, nefes, can, kalp, inilti...");
        Drone(0.06);
        Breath(0.03);
        Bell(0, 38, 0.06);
        Bell(15, 38, 0.04);
        Bell(30, 39, 0.06);
        Bell(45, 37, 0.04);
        for (int k = 0; k < 50; k++)
        {
            double t = k * 1.2;
            double sw = 0.6 + 0.4 * Math.Sin(2 * Math.PI * t * 2.0 / LEN);
            Heart(t, 0.2 * sw);
            Heart(t + 0.32, 0.12 * sw);
        }
        Moan(23, 45, 38, 5.5, 0.022, -0.6);
        Moan(53, 44, 39, 5.0, 0.02, 0.6);

        Console.WriteLine("Dort ses...");
        PlayCelloLine(VBass, 0.07, 0.0);                  // bas
        PlayChoirLine(VTenor, 0.05, -0.45, 450, 800);     // tenor - "oh"
        PlayCelloLine(VAlto, 0.045, 0.45);                // alto - viyola
        PlayChoirLine(VTop, 0.04, -0.15, 320, 870);       // ust ses - "oo"

        Console.WriteLine("Reverb...");
        float[] wl = Reverb(SL, 0), wr = Reverb(SR_, 23);

        Console.WriteLine("Mix + yazma...");
        double[] ol = new double[N], or_ = new double[N];
        for (int i = 0; i < N; i++) { ol[i] = L[i] + wl[i] * 1.0; or_[i] = R[i] + wr[i] * 1.0; }
        LowpassCircular(ol, 2800); LowpassCircular(or_, 2800);
        double peak = 0;
        for (int i = 0; i < N; i++)
        {
            ol[i] = Math.Tanh(ol[i] * 1.2); or_[i] = Math.Tanh(or_[i] * 1.2);
            peak = Math.Max(peak, Math.Max(Math.Abs(ol[i]), Math.Abs(or_[i])));
        }
        WriteWav(path, ol, or_, 0.85 / peak);
        Console.WriteLine("Tamam: " + path + "  (" + (N / (double)SR) + " sn, tepe " + peak.ToString("F3") + ")");
    }

    static void WriteWav(string path, double[] l, double[] r, double g)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using (BinaryWriter w = new BinaryWriter(File.Create(path)))
        {
            int dataLen = N * 4;
            w.Write(new char[] { 'R', 'I', 'F', 'F' }); w.Write(36 + dataLen);
            w.Write(new char[] { 'W', 'A', 'V', 'E' });
            w.Write(new char[] { 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)2);
            w.Write(SR); w.Write(SR * 4); w.Write((short)4); w.Write((short)16);
            w.Write(new char[] { 'd', 'a', 't', 'a' }); w.Write(dataLen);
            for (int i = 0; i < N; i++)
            {
                w.Write((short)Math.Round(Math.Max(-1, Math.Min(1, l[i] * g)) * 32767));
                w.Write((short)Math.Round(Math.Max(-1, Math.Min(1, r[i] * g)) * 32767));
            }
        }
    }
}
