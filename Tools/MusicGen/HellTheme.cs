// FarToHell - "Cehennemde Saskinlik" ana temasi
// 60 saniyelik, kusursuz donguye giren (seamless loop) prosedurel muzik ureticisi.
// Calistirma: Tools/MusicGen/generate.ps1
// Ton: D Frigyen / armonik minor, tam-ton kaymalari ve egilen notalar ile "saskin" bir hava.
using System;
using System.IO;

public static class HellTheme
{
    const int SR = 44100;
    const double BPM = 96.0;
    const double BEAT = 60.0 / BPM;   // 0.625 sn
    const int BARS = 24;              // 24 olcu * 4 vurus * 0.625 = 60 sn
    static int N;
    static float[] L, R, SL, SR_;     // kuru sinyal + reverb gonderimi
    static Random rng = new Random(666);
    static double send = 0.3;

    static double Mtof(double m) { return 440.0 * Math.Pow(2.0, (m - 69.0) / 12.0); }
    // Surekli osilatorler icin frekansi 1/60 Hz katina yuvarla -> dongu noktasinda faz kirilmasi olmaz
    static double Q(double f) { return Math.Round(f * 60.0) / 60.0; }

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

    // ---------------- ENSTRUMANLAR ----------------

    // Derin cehennem dronu: D1 + A1, yavas nefes alan parlaklik
    static void Drone(double amp)
    {
        send = 0.25;
        double[] fl = { Q(Mtof(26)), Q(Mtof(33)) };
        double[] fr = { Q(Mtof(26.08)), Q(Mtof(33.07)) };
        double[] w = { 1.0, 0.45 };
        for (int i = 0; i < N; i++)
        {
            double t = (double)i / SR;
            double bright = 0.38 - 0.16 * Math.Sin(2 * Math.PI * t * 2.0 / 60.0);
            double am = 0.75 + 0.25 * Math.Sin(2 * Math.PI * t * 4.0 / 60.0);
            double l = 0, r = 0;
            for (int v = 0; v < 2; v++)
                for (int h = 1; h <= 10; h++)
                {
                    double g = w[v] * Math.Exp(-h * bright) / h;
                    l += g * Math.Sin(2 * Math.PI * fl[v] * h * t);
                    r += g * Math.Sin(2 * Math.PI * fr[v] * h * t);
                }
            PutLR(i, l * am * amp, r * am * amp);
        }
    }

    // Detune edilmis testere padleri, iki kutuplu alcak geciren filtre
    static void PadChord(double startBeat, double lenBeats, int[] notes, double amp)
    {
        send = 0.55;
        double dur = lenBeats * BEAT, att = 1.1, rel = 1.8, total = dur + rel;
        int s0 = (int)(startBeat * BEAT * SR), n = (int)(total * SR);
        double[] det = { -0.09, 0.0, 0.1 };
        for (int j = 0; j < notes.Length; j++)
            for (int d = 0; d < det.Length; d++)
            {
                double f = Mtof(notes[j] + det[d]);
                double pan = (j - (notes.Length - 1) / 2.0) * 0.45 + det[d] * 3.0;
                double ph = rng.NextDouble(), y1 = 0, y2 = 0;
                for (int i = 0; i < n; i++)
                {
                    double t = (double)i / SR;
                    ph += f / SR; if (ph >= 1) ph -= 1;
                    double x = 2 * ph - 1;
                    double fc = 350 + 900 * Math.Min(t / dur, 1.0);
                    double a = 1 - Math.Exp(-2 * Math.PI * fc / SR);
                    y1 += a * (x - y1); y2 += a * (y1 - y2);
                    double e = t < att ? t / att : (t < dur ? 1.0 : Math.Max(0, 1 - (t - dur) / rel));
                    e = e * e * (3 - 2 * e);
                    Put(s0 + i, y2 * e * amp, pan);
                }
            }
    }

    // Kalp atisi gibi "lub-dub" davul
    static void Heart(double startBeat, double amp)
    {
        send = 0.12;
        int s0 = (int)(startBeat * BEAT * SR), n = (int)(0.6 * SR);
        double ph = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / SR;
            double f = 33 + 45 * Math.Exp(-t * 28);
            ph += 2 * Math.PI * f / SR;
            double v = Math.Sin(ph) * Math.Exp(-t * 8) * Math.Min(t / 0.002, 1);
            Put(s0 + i, Math.Tanh(v * 1.6) * amp, 0);
        }
    }

    // Hafif dist. bas
    static void Bass(double startBeat, double midi, double lenBeats, double amp)
    {
        send = 0.08;
        double dur = lenBeats * BEAT, rel = 0.12;
        int s0 = (int)(startBeat * BEAT * SR), n = (int)((dur + rel) * SR);
        double f = Mtof(midi), ph = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / SR;
            ph += 2 * Math.PI * f / SR;
            double v = Math.Sin(ph) + 0.35 * Math.Sin(2 * ph) + 0.15 * Math.Sin(3 * ph);
            v = Math.Tanh(v * 1.4);
            double e = (0.6 + 0.4 * Math.Exp(-t * 6)) * Math.Min(t / 0.008, 1);
            if (t > dur) e *= Math.Max(0, 1 - (t - dur) / rel);
            Put(s0 + i, v * e * amp, 0);
        }
    }

    // Muzik kutusu / celesta - saskinlik icin vibrato ve pitch bend
    static void MusicBox(double startBeat, double midi, double lenBeats, double bend, double amp, double pan)
    {
        send = 0.45;
        double noteLen = lenBeats * BEAT;
        double total = Math.Max(noteLen, 0.3) + 1.8;
        int s0 = (int)(startBeat * BEAT * SR), n = (int)(total * SR);
        double f = Mtof(midi), p1 = 0, p2 = 0, p3 = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / SR;
            double b = 0;
            if (bend != 0) { double x = Math.Min(t / Math.Max(noteLen, 0.05), 1.0); b = bend * x * x; }
            double vib = 0.13 * Math.Sin(2 * Math.PI * 5.2 * t) * Math.Min(t / 0.35, 1);
            double ff = f * Math.Pow(2, (b + vib) / 12.0);
            p1 += 2 * Math.PI * ff / SR; p2 += 2 * Math.PI * ff * 2.0 / SR; p3 += 2 * Math.PI * ff * 4.07 / SR;
            double v = Math.Sin(p1) * Math.Exp(-t * 2.2)
                     + 0.3 * Math.Sin(p2) * Math.Exp(-t * 4.0)
                     + 0.12 * Math.Sin(p3) * Math.Exp(-t * 9.0);
            double e = Math.Min(t / 0.004, 1) * Math.Min((total - t) / 0.3, 1);
            Put(s0 + i, v * e * amp, pan);
        }
    }

    // Koyu, yayli/cello benzeri lead: detune testereler + alt oktav, yavas atak, vibrato ve bend
    static void DarkLead(double startBeat, double midi, double lenBeats, double bend, double amp, double pan)
    {
        send = 0.5;
        double noteLen = lenBeats * BEAT, att = 0.09, rel = 0.6, total = noteLen + rel;
        int s0 = (int)(startBeat * BEAT * SR), n = (int)(total * SR);
        double f = Mtof(midi);
        double pa = rng.NextDouble(), pb = rng.NextDouble(), ps = 0;
        double y1 = 0, y2 = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / SR;
            double b = 0;
            if (bend != 0) { double x = Math.Min(t / Math.Max(noteLen, 0.05), 1.0); b = bend * x * x; }
            double vib = 0.15 * Math.Sin(2 * Math.PI * 4.6 * t) * Math.Min(t / 0.4, 1);
            double ff = f * Math.Pow(2, (b + vib) / 12.0);
            pa += ff * 1.004 / SR; if (pa >= 1) pa -= 1;
            pb += ff * 0.996 / SR; if (pb >= 1) pb -= 1;
            ps += 2 * Math.PI * ff * 0.5 / SR;
            double x0 = (2 * pa - 1) + (2 * pb - 1);
            double e = t < att ? t / att : (t < noteLen ? 1.0 - 0.15 * Math.Min((t - att) / 0.5, 1) : 0.85 * Math.Max(0, 1 - (t - noteLen) / rel));
            double fc = 300 + 900 * e;
            double a = 1 - Math.Exp(-2 * Math.PI * fc / SR);
            y1 += a * (x0 - y1); y2 += a * (y1 - y2);
            double v = y2 * 0.6 + 0.35 * Math.Sin(ps);
            Put(s0 + i, v * e * amp, pan);
        }
    }

    // Ping-pong yankili muzik kutusu
    static void MusicBoxEcho(double beat, double midi, double len, double bend, double amp, double pan)
    {
        MusicBox(beat, midi, len, bend, amp, pan);
        MusicBox(beat + 0.75, midi, len, bend, amp * 0.33, -pan * 0.9 - 0.3);
        MusicBox(beat + 1.5, midi, len, bend, amp * 0.13, pan * 0.9 + 0.3);
    }

    // Ruhani "theremin" - kayan, titreyen sinus melodisi
    static void Theremin(double baseBeat, double[,] notes, double amp)
    {
        send = 0.6;
        int cnt = notes.GetLength(0);
        double startT = (baseBeat + notes[0, 0]) * BEAT;
        double endT = (baseBeat + notes[cnt - 1, 0] + notes[cnt - 1, 2]) * BEAT;
        int s0 = (int)(startT * SR), n = (int)((endT - startT + 0.6) * SR);
        double ph = 0, cur = notes[0, 1];
        int idx = 0;
        for (int i = 0; i < n; i++)
        {
            double t = startT + (double)i / SR;
            double beat = t / BEAT - baseBeat;
            while (idx < cnt - 1 && beat >= notes[idx + 1, 0]) idx++;
            double target = notes[idx, 1];
            cur += (target - cur) * (1 - Math.Exp(-1.0 / (0.07 * SR)));
            double lt = (double)i / SR;
            double rate = 5.0 + 1.5 * Math.Sin(2 * Math.PI * 0.3 * lt);
            double vib = 0.28 * Math.Sin(2 * Math.PI * rate * lt) * Math.Min(lt / 0.5, 1);
            double f = Mtof(cur + vib);
            ph += 2 * Math.PI * f / SR;
            double v = Math.Sin(ph) + 0.15 * Math.Sin(2 * ph) + 0.05 * Math.Sin(3 * ph);
            double noteT = (beat - notes[idx, 0]) * BEAT;
            double e = (0.8 + 0.2 * Math.Min(noteT / 0.12, 1));
            e *= Math.Min(lt / 0.4, 1);
            double tailT = t - endT;
            if (tailT > 0) e *= Math.Max(0, 1 - tailT / 0.6);
            double pan = 0.4 * Math.Sin(2 * Math.PI * 0.1 * lt);
            Put(s0 + i, v * e * amp, pan);
        }
    }

    // Uyumsuz (inharmonik) gong - cehennem kapisi
    static void Gong(double startBeat, double baseMidi, double amp)
    {
        send = 0.7;
        double[] ratio = { 1, 1.52, 2.17, 2.71, 3.3, 4.16, 5.43 };
        double[] am = { 1, .7, .5, .4, .3, .2, .15 };
        double[] dec = { 0.6, 0.8, 1.1, 1.4, 1.8, 2.5, 3.2 };
        double f0 = Mtof(baseMidi);
        int s0 = (int)(startBeat * BEAT * SR), n = (int)(7.0 * SR);
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / SR, l = 0, r = 0;
            for (int p = 0; p < ratio.Length; p++)
            {
                double f = f0 * ratio[p], g = am[p] * Math.Exp(-t * dec[p]);
                l += g * Math.Sin(2 * Math.PI * (f - 0.3) * t);
                r += g * Math.Sin(2 * Math.PI * (f + 0.3) * t);
            }
            double e = Math.Min(t / 0.02, 1) * Math.Min((7.0 - t) / 0.5, 1);
            PutLR(s0 + i, l * e * amp, r * e * amp);
        }
    }

    // Tik-tak saat (biraz aksayan - zaman bile sasirmis)
    static void Tick(double startBeat, double freq, double amp, double pan)
    {
        send = 0.35;
        int s0 = (int)(startBeat * BEAT * SR), n = (int)(0.06 * SR);
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / SR;
            double v = Math.Sin(2 * Math.PI * freq * t) * Math.Exp(-t * 150) + (rng.NextDouble() * 2 - 1) * 0.3 * Math.Exp(-t * 400);
            Put(s0 + i, v * amp, pan);
        }
    }

    // Ates citirtisi + derin ugultu
    static void Fire(double crackleAmp, double rumbleRms)
    {
        send = 0.2;
        int count = 60 * 3;
        for (int c = 0; c < count; c++)
        {
            int s0 = rng.Next(N);
            int n = (int)((0.002 + rng.NextDouble() * 0.008) * SR);
            double a = crackleAmp * (0.3 + rng.NextDouble() * 0.7), pan = rng.NextDouble() * 1.6 - 0.8, prev = 0;
            for (int i = 0; i < n; i++)
            {
                double x = rng.NextDouble() * 2 - 1, hp = x - prev; prev = x;
                Put(s0 + i, hp * a * Math.Exp(-(double)i / n * 4), pan);
            }
        }
        // ugultu: filtrelenmis gurultu, iki tur dolasip dongu sinirini yumusat
        float[] rl = new float[N], rr = new float[N];
        double a1 = 1 - Math.Exp(-2 * Math.PI * 110.0 / SR);
        double l1 = 0, l2 = 0, r1 = 0, r2 = 0;
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < N; i++)
            {
                l1 += a1 * (rng.NextDouble() * 2 - 1 - l1); l2 += a1 * (l1 - l2);
                r1 += a1 * (rng.NextDouble() * 2 - 1 - r1); r2 += a1 * (r1 - r2);
                if (pass == 1) { rl[i] = (float)l2; rr[i] = (float)r2; }
            }
        double sum = 0;
        for (int i = 0; i < N; i++) sum += rl[i] * rl[i];
        double g = rumbleRms / Math.Sqrt(sum / N);
        send = 0.1;
        for (int i = 0; i < N; i++)
        {
            double t = (double)i / SR;
            double sw = 0.7 + 0.3 * Math.Sin(2 * Math.PI * t * 3.0 / 60.0);
            PutLR(i, rl[i] * g * sw, rr[i] * g * sw);
        }
    }

    // Dairesel Freeverb benzeri reverb (iki tur -> dongu kusursuz)
    static float[] Reverb(float[] input, int spread)
    {
        int[] cd = { 1557, 1617, 1491, 1422, 1277, 1356, 1188, 1116 };
        int[] ad = { 556, 441, 341, 225 };
        double fb = 0.9, damp = 0.3, g = 0.5;
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
                    double outv = -s + bo;
                    ab[c][ai[c]] = (float)(s + bo * g);
                    if (++ai[c] >= ab[c].Length) ai[c] = 0;
                    s = outv;
                }
                if (pass == 1) o[i] = (float)s;
            }
        return o;
    }

    // ---------------- BESTE ----------------

    // A bolumu muzik kutusu melodisi: {vurus, midi, uzunluk, bend}
    static readonly double[,] MelodyA = {
        {0,69,1,0},{1,74,.5,0},{1.5,77,.5,0},{2,76,1.5,-0.8},{4,73,.5,0},{4.5,74,.5,0},{5,69,2.5,0},
        {8,79,1,0},{9,82,.5,0},{9.5,81,.5,0},{10,79,1.5,-0.6},{12,75,.5,0},{12.5,74,.5,0},{13,75,2,0},
        {16,76,.5,0},{16.5,80,.5,0},{17,84,1,0},{18,83,.5,0},{18.5,80,.5,0},{19,76,1,0},
        {20,74,.5,0},{20.5,72,.5,0},{21,70,.5,0},{21.5,68,.5,0},{22,66,1.5,-1.0},
        {24,70,1,0},{25,69,1,0},{26,73,.5,0},{26.5,76,.5,0},{27,79,1,0},{28,82,1.5,0.7},
        {29.5,81,.5,0},{30,77,.5,0},{30.5,76,.5,0},{31,73,1,0}
    };

    // B bolumu theremin melodisi: {vurus, midi, uzunluk}
    static readonly double[,] MelodyB = {
        {0,74,3},{3,77,1},{4,76,4},{8,75,3},{11,79,1},{12,78,4},
        {16,76,2},{18,80,2},{20,84,2},{22,82,2},{24,81,4},{28,82,2},{30,85,2}
    };

    public static void Run(string path)
    {
        N = (int)Math.Round(BARS * 4 * BEAT * SR);
        L = new float[N]; R = new float[N]; SL = new float[N]; SR_ = new float[N];

        // 8 olculuk dongu: Dm | Eb | C+ (artik) | A7b9
        int[][] chords = {
            new int[]{50,53,57}, new int[]{51,55,58}, new int[]{48,52,56}, new int[]{49,55,58}
        };
        int[] roots = { 38, 39, 36, 33 };

        Console.WriteLine("Dron...");
        Drone(0.05);
        Console.WriteLine("Ates...");
        Fire(0.03, 0.025);

        Console.WriteLine("Pad, bas, kalp...");
        for (int bar = 0; bar < BARS; bar++)
        {
            int ch = (bar / 2) % 4;
            double b0 = bar * 4;
            if (bar % 2 == 0) PadChord(b0, 8, chords[ch], bar >= 8 && bar < 16 ? 0.022 : 0.03);
            int root = roots[ch];
            Bass(b0, root, 1.2, 0.16);
            Bass(b0 + 1.5, root, 0.4, 0.12);
            Bass(b0 + 2, root, 1.0, 0.14);
            Bass(b0 + 3.5, bar % 2 == 0 ? root + 1 : root + 12, 0.4, 0.11);
            Heart(b0, 0.42);
            Heart(b0 + 0.55, 0.28);
        }

        Console.WriteLine("Gonglar...");
        Gong(0, 38, 0.06);
        Gong(32, 39, 0.05);
        Gong(64, 38, 0.06);

        Console.WriteLine("Koyu lead...");
        for (int sec = 0; sec < 2; sec++)
        {
            double baseBeat = sec == 0 ? 0 : 64;
            for (int k = 0; k < MelodyA.GetLength(0); k++)
            {
                double pan = (k % 2 == 0 ? -0.15 : 0.15);
                // bir oktav asagi: daha koyu
                DarkLead(baseBeat + MelodyA[k, 0], MelodyA[k, 1] - 12, MelodyA[k, 2], MelodyA[k, 3], 0.11, pan);
            }
        }

        Console.WriteLine("Theremin...");
        Theremin(32, MelodyB, 0.075);

        Console.WriteLine("Saat...");
        for (int bt = 32; bt < 96; bt++)
        {
            double lag = rng.NextDouble() < 0.2 ? 0.08 : 0.0;
            bool tick = bt % 2 == 0;
            Tick(bt + lag, tick ? 2400 : 1700, 0.03, tick ? -0.6 : 0.6);
        }

        Console.WriteLine("Reverb...");
        float[] wl = Reverb(SL, 0), wr = Reverb(SR_, 23);

        Console.WriteLine("Mix + yazma...");
        double peak = 0;
        double[] ol = new double[N], or_ = new double[N];
        for (int i = 0; i < N; i++)
        {
            ol[i] = Math.Tanh((L[i] + wl[i] * 0.9) * 1.3);
            or_[i] = Math.Tanh((R[i] + wr[i] * 0.9) * 1.3);
            peak = Math.Max(peak, Math.Max(Math.Abs(ol[i]), Math.Abs(or_[i])));
        }
        double norm = 0.89 / peak;
        WriteWav(path, ol, or_, norm);
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
