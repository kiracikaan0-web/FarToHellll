// FarToHell - "Hell Gallop": NWOBHM tarzi, ozgun bir enstrumantal metal dongusu.
// Dortnala (gallop) ritim gitarlari, terslerle armonize ikiz lead gitarlar, surukleyici davul.
// 160 BPM, E minor, 40 olcu = tam 60 sn, kusursuz dongu.
using System;
using System.IO;

public static class HellGallop
{
    const int SR = 44100;
    static double BPM = 160.0;
    static double BEAT = 60.0 / BPM;  // 0.375 sn
    static int BARS = 40;
    static int N;
    static float[] L, R, SL, SR_;
    static Random rng = new Random(1666);
    static double send = 0.1;

    static double Mtof(double m) { return 440.0 * Math.Pow(2.0, (m - 69.0) / 12.0); }
    static double Coef(double fc) { return 1 - Math.Exp(-2 * Math.PI * fc / SR); }

    static void Put(int i, double v, double pan)
    {
        int k = ((i % N) + N) % N;
        double a = (pan + 1.0) * Math.PI / 4.0;
        double l = v * Math.Cos(a), r = v * Math.Sin(a);
        L[k] += (float)l; R[k] += (float)r;
        SL[k] += (float)(l * send); SR_[k] += (float)(r * send);
    }

    // ---------------- GITARLAR ----------------

    // Distorsiyonlu ritim gitar (power chord). muted = palm mute
    static void Rhythm(double startBeat, int root, double lenBeats, bool muted, double amp, double pan, double cents, double offSec)
    {
        send = 0.07;
        double noteLen = lenBeats * BEAT * (muted ? 0.85 : 0.95);
        double rel = muted ? 0.03 : 0.12;
        int s0 = (int)(startBeat * BEAT * SR + offSec * SR), n = (int)((noteLen + rel) * SR);
        double[] fr = { Mtof(root + cents / 100.0), Mtof(root + 7 + cents / 100.0), Mtof(root + 12 + cents / 100.0) };
        double[] ph = { rng.NextDouble(), rng.NextDouble(), rng.NextDouble() };
        double pre1 = 0, hp = 0, c1 = 0, c2 = 0, c3 = 0;
        double aPre = Coef(2200), aHp = Coef(140), aCab = Coef(muted ? 1500 : 3600), aCab2 = Coef(5500);
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / SR;
            double x = 0;
            for (int k = 0; k < 3; k++) { ph[k] += fr[k] / SR; if (ph[k] >= 1) ph[k] -= 1; x += (2 * ph[k] - 1) * (k == 2 ? 0.6 : 1.0); }
            pre1 += aPre * (x - pre1);
            hp += aHp * (pre1 - hp);
            double d = Math.Tanh((pre1 - hp) * 14.0);
            c1 += aCab * (d - c1); c2 += aCab * (c1 - c2); c3 += aCab2 * (c2 - c3);
            double e = Math.Min(t / 0.003, 1);
            if (muted) e *= 0.55 + 0.45 * Math.Exp(-t * 14);
            if (t > noteLen) e *= Math.Max(0, 1 - (t - noteLen) / rel);
            Put(s0 + i, c3 * e * amp, pan);
        }
    }

    // Lead gitar: tek nota, kayarak giris, vibrato
    static void Lead(double startBeat, double midi, double lenBeats, double amp, double pan, double offSec)
    {
        send = 0.3;
        double noteLen = lenBeats * BEAT, rel = 0.06;
        int s0 = (int)(startBeat * BEAT * SR + offSec * SR), n = (int)((noteLen + rel) * SR);
        double ph = rng.NextDouble(), pre = 0, hp = 0, c1 = 0, c2 = 0;
        double aPre = Coef(2600), aHp = Coef(250), aCab = Coef(4200);
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / SR;
            double slide = -0.8 * Math.Max(0, 1 - t / 0.03);
            double vib = noteLen > 0.35 ? 0.3 * Math.Sin(2 * Math.PI * 5.6 * t) * Math.Min(Math.Max(t - 0.15, 0) / 0.15, 1) : 0;
            double f = Mtof(midi + slide + vib);
            ph += f / SR; if (ph >= 1) ph -= 1;
            double x = (2 * ph - 1) + 0.4 * (ph < 0.5 ? 1 : -1);
            pre += aPre * (x - pre);
            hp += aHp * (pre - hp);
            double d = Math.Tanh((pre - hp) * 22.0);
            c1 += aCab * (d - c1); c2 += aCab * (c1 - c2);
            double e = Math.Min(t / 0.004, 1);
            if (t > noteLen) e *= Math.Max(0, 1 - (t - noteLen) / rel);
            Put(s0 + i, c2 * e * amp, pan);
        }
    }

    // Bas gitar
    static void Bass(double startBeat, int midi, double lenBeats, double amp)
    {
        send = 0.03;
        double noteLen = lenBeats * BEAT * 0.9, rel = 0.04;
        int s0 = (int)(startBeat * BEAT * SR), n = (int)((noteLen + rel) * SR);
        double f = Mtof(midi), ph = 0, lp = 0, a = Coef(900);
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / SR;
            ph += f / SR; if (ph >= 1) ph -= 1;
            double saw = 2 * ph - 1;
            lp += a * (saw - lp);
            double v = Math.Tanh((lp * 0.8 + 0.6 * Math.Sin(2 * Math.PI * ph)) * 1.5);
            double e = Math.Min(t / 0.003, 1) * (0.7 + 0.3 * Math.Exp(-t * 10));
            if (t > noteLen) e *= Math.Max(0, 1 - (t - noteLen) / rel);
            Put(s0 + i, v * e * amp, 0);
        }
    }

    // ---------------- DAVUL ----------------

    static void Kick(double beat, double amp)
    {
        send = 0.05;
        int s0 = (int)(beat * BEAT * SR), n = (int)(0.3 * SR);
        double ph = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / SR;
            ph += 2 * Math.PI * (48 + 130 * Math.Exp(-t * 40)) / SR;
            double v = Math.Sin(ph) * Math.Exp(-t * 11) + (rng.NextDouble() * 2 - 1) * 0.25 * Math.Exp(-t * 350);
            Put(s0 + i, Math.Tanh(v * 1.3) * amp, 0);
        }
    }

    static void Snare(double beat, double amp)
    {
        send = 0.3;
        int s0 = (int)(beat * BEAT * SR), n = (int)(0.35 * SR);
        double prev = 0, ph = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / SR;
            ph += 2 * Math.PI * (190 + 40 * Math.Exp(-t * 60)) / SR;
            double x = rng.NextDouble() * 2 - 1, hpn = x - prev * 0.6; prev = x;
            double v = 0.55 * Math.Sin(ph) * Math.Exp(-t * 22) + 0.7 * hpn * Math.Exp(-t * 15);
            Put(s0 + i, v * amp, 0.05);
        }
    }

    static void Hat(double beat, double amp, bool open)
    {
        send = 0.08;
        int s0 = (int)(beat * BEAT * SR), n = (int)((open ? 0.25 : 0.06) * SR);
        double p1 = 0, p2 = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / SR;
            double x = rng.NextDouble() * 2 - 1;
            double h1 = x - p1; p1 = x; double h2 = h1 - p2; p2 = h1;
            Put(s0 + i, h2 * 0.5 * amp * Math.Exp(-t * (open ? 12 : 60)), 0.35);
        }
    }

    static void Crash(double beat, double amp)
    {
        send = 0.35;
        int s0 = (int)(beat * BEAT * SR), n = (int)(2.6 * SR);
        double p1 = 0;
        double[] fr = { 3150, 4270, 5110, 6830, 7410 };
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / SR;
            double x = rng.NextDouble() * 2 - 1, h = x - p1; p1 = x;
            double m = 0;
            for (int k = 0; k < fr.Length; k++) m += Math.Sin(2 * Math.PI * fr[k] * t);
            double v = (h * 0.6 + m * 0.04) * Math.Exp(-t * 2.4) * Math.Min(t / 0.002, 1) * Math.Min((2.6 - t) / 0.2, 1);
            Put(s0 + i, v * amp, -0.35);
        }
    }

    static void Tom(double beat, int midi, double amp)
    {
        send = 0.2;
        int s0 = (int)(beat * BEAT * SR), n = (int)(0.4 * SR);
        double f = Mtof(midi), ph = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / SR;
            ph += 2 * Math.PI * f * (1 + 0.5 * Math.Exp(-t * 30)) / SR;
            double v = Math.Sin(ph) * Math.Exp(-t * 9) + (rng.NextDouble() * 2 - 1) * 0.15 * Math.Exp(-t * 80);
            Put(s0 + i, v * amp, (midi - 45) * 0.08);
        }
    }

    // ---------------- REVERB ----------------
    static float[] Reverb(float[] input, int spread)
    {
        int[] cd = { 1557, 1617, 1491, 1422, 1277, 1356 };
        int[] ad = { 556, 441, 341 };
        double fb = 0.8, damp = 0.35, g = 0.5;
        float[][] cb = new float[cd.Length][]; int[] ci = new int[cd.Length]; double[] cf = new double[cd.Length];
        for (int c = 0; c < cd.Length; c++) cb[c] = new float[cd[c] + spread];
        float[][] ab = new float[ad.Length][]; int[] ai = new int[ad.Length];
        for (int c = 0; c < ad.Length; c++) ab[c] = new float[ad[c] + spread];
        float[] o = new float[N];
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < N; i++)
            {
                double x = input[i] * 0.12, s = 0;
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

    // ---------------- BESTE ----------------

    // E minor dereceleri (E'den yari ses); armonik minor icin 7. derece D#
    static readonly int[] Natural = { 0, 2, 3, 5, 7, 8, 10 };
    static readonly int[] Harmonic = { 0, 2, 3, 5, 7, 8, 11 };

    // Diyatonik ters asagi (melodinin altindan ikinci gitar)
    static double ThirdBelow(double midi, bool harmonic)
    {
        int[] sc = harmonic ? Harmonic : Natural;
        int m = (int)midi;
        int rel = ((m - 64) % 12 + 12) % 12;
        int oct = (int)Math.Floor((m - 64) / 12.0);
        int idx = Array.IndexOf(sc, rel);
        if (idx < 0) return midi - 3;
        int j = idx - 2, o = oct;
        if (j < 0) { j += 7; o -= 1; }
        return 64 + 12 * o + sc[j];
    }

    // Melodi 1: {vurus, midi, uzunluk} - Em | C | D | B, her biri 2 olcu
    static readonly double[,] Mel1 = {
        {0,71,.5},{.5,76,1},{1.5,74,.5},{2,76,.5},{2.5,79,1.5},{4,78,.5},{4.5,76,.5},{5,74,.5},{5.5,71,2.5},
        {8,72,.5},{8.5,76,1},{9.5,79,.5},{10,76,.5},{10.5,72,.5},{11,76,1},{12,79,.5},{12.5,81,.5},{13,79,.5},{13.5,76,.5},{14,79,2},
        {16,81,1},{17,78,.5},{17.5,74,.5},{18,78,.5},{18.5,81,.5},{19,83,1},{20,81,.5},{20.5,79,.5},{21,78,.5},{21.5,76,.5},{22,74,2},
        {24,75,1},{25,78,1},{26,83,1.5},{27.5,81,.5},{28,79,.5},{28.5,78,.5},{29,76,.5},{29.5,75,.5},{30,78,2}
    };

    // Melodi 2: daha hareketli, 16'lik kosular
    static readonly double[,] Mel2 = {
        {0,76,.25},{.25,78,.25},{.5,79,.5},{1,78,.25},{1.25,76,.25},{1.5,71,.5},{2,76,.5},{2.5,79,.5},{3,83,1},{4,81,.5},{4.5,79,.5},{5,78,.5},{5.5,79,.5},{6,76,2},
        {8,76,.25},{8.25,78,.25},{8.5,79,.5},{9,84,1},{10,83,.5},{10.5,81,.5},{11,79,.5},{11.5,76,.5},{12,72,.5},{12.5,76,.5},{13,79,1},{14,76,2},
        {16,78,.25},{16.25,79,.25},{16.5,81,.5},{17,86,1},{18,84,.5},{18.5,83,.5},{19,81,.5},{19.5,78,.5},{20,74,.5},{20.5,78,.5},{21,81,1},{22,78,2},
        {24,83,1.5},{25.5,81,.5},{26,79,.5},{26.5,78,.5},{27,76,.5},{27.5,75,.5},{28,76,.5},{28.5,78,.5},{29,79,.5},{29.5,81,.5},{30,83,2}
    };

    static void PlayMelody(double baseBeat, double[,] mel, bool twin, bool octaveDouble, double amp)
    {
        for (int k = 0; k < mel.GetLength(0); k++)
        {
            double b = mel[k, 0], m = mel[k, 1], len = mel[k, 2];
            bool harm = b >= 24; // B akoru: armonik minor
            Lead(baseBeat + b, m, len, amp, twin ? 0.35 : 0.15, 0);
            if (twin) Lead(baseBeat + b, ThirdBelow(m, harm), len, amp * 0.9, -0.35, 0.004);
            if (octaveDouble) Lead(baseBeat + b, m - 12, len, amp * 0.55, 0, 0.008);
        }
    }

    static void GallopBar(double b0, int root, double gAmp, double bAmp)
    {
        for (int bt = 0; bt < 4; bt++)
        {
            double b = b0 + bt;
            bool accent = bt == 0;
            double[] off = { 0, 0.5, 0.75 };
            double[] len = { 0.45, 0.22, 0.22 };
            for (int h = 0; h < 3; h++)
            {
                bool muted = !(accent && h == 0);
                Rhythm(b + off[h], root, len[h], muted, gAmp, -0.75, -6, 0);
                Rhythm(b + off[h], root, len[h], muted, gAmp, 0.75, 7, 0.006);
                Bass(b + off[h], root - 12, len[h], bAmp);
            }
        }
    }

    public static void Run(string path)
    {
        N = (int)Math.Round(BARS * 4 * BEAT * SR);
        L = new float[N]; R = new float[N]; SL = new float[N]; SR_ = new float[N];

        int[] prog = { 40, 48, 50, 47 };                 // E5 | C5 | D5 | B5 (her biri 2 olcu)
        int[] verse = { 40, 40, 41, 40, 40, 40, 43, 42 }; // E E F E E E G F#
        double gA = 0.085, bA = 0.16;

        Console.WriteLine("Ritim + bas + davul...");
        for (int bar = 0; bar < BARS; bar++)
        {
            double b0 = bar * 4;
            int sec = bar / 8;                // 0 intro, 1 verse, 2 lead, 3 breakdown, 4 final
            int inSec = bar % 8;
            int root = sec == 1 ? verse[inSec] : prog[inSec / 2];

            if (sec == 3)
            {
                // yari tempo: uzun akorlar
                Rhythm(b0, root, 3.4, false, gA, -0.75, -6, 0);
                Rhythm(b0, root, 3.4, false, gA, 0.75, 7, 0.006);
                Rhythm(b0 + 3.5, root, 0.3, true, gA, -0.75, -6, 0);
                Rhythm(b0 + 3.5, root, 0.3, true, gA, 0.75, 7, 0.006);
                Bass(b0, root - 12, 3.4, bA);
                Bass(b0 + 3.5, root - 12, 0.3, bA);
                Kick(b0, 0.5); Snare(b0 + 2, 0.36);
                if (inSec != 7) Kick(b0 + 3.5, 0.4);
                for (int q = 0; q < 4; q++) Hat(b0 + q, 0.07, q == 3);
                if (inSec == 7)
                {
                    int[] toms = { 52, 52, 50, 50, 47, 47, 43, 43 };
                    for (int k = 0; k < 8; k++) Tom(b0 + 2 + k * 0.25, toms[k], 0.3 + k * 0.02);
                }
            }
            else
            {
                GallopBar(b0, root, gA, bA);
                for (int q = 0; q < 4; q++)
                {
                    if (sec == 1) { Kick(b0 + q, 0.48); Kick(b0 + q + 0.5, 0.35); Kick(b0 + q + 0.75, 0.35); }
                    else Kick(b0 + q, 0.5);
                    Hat(b0 + q, 0.07, false); Hat(b0 + q + 0.5, 0.05, false);
                }
                bool fill = inSec == 7;
                Snare(b0 + 1, 0.36);
                if (fill) for (int k = 0; k < 4; k++) Snare(b0 + 3 + k * 0.25, 0.22 + k * 0.05);
                else Snare(b0 + 3, 0.36);
            }

            if (inSec == 0) Crash(b0, sec == 4 ? 0.16 : 0.13);
            if (sec == 4 && inSec == 4) Crash(b0, 0.12);
        }

        Console.WriteLine("Lead gitarlar...");
        PlayMelody(0, Mel1, true, false, 0.075);        // 0-7: ikiz lead tema
        PlayMelody(64, Mel2, true, false, 0.075);       // 16-23: hareketli ikiz lead
        PlayMelody(96, Mel1, false, false, 0.08);       // 24-31: tek gitar, sakin kisim
        PlayMelody(128, Mel2, true, true, 0.075);       // 32-39: final, oktav takviyeli

        Console.WriteLine("Reverb...");
        float[] wl = Reverb(SL, 0), wr = Reverb(SR_, 23);

        Console.WriteLine("Mix + yazma...");
        double peak = 0;
        double[] ol = new double[N], or_ = new double[N];
        for (int i = 0; i < N; i++)
        {
            ol[i] = Math.Tanh((L[i] + wl[i] * 0.8) * 1.2);
            or_[i] = Math.Tanh((R[i] + wr[i] * 0.8) * 1.2);
            peak = Math.Max(peak, Math.Max(Math.Abs(ol[i]), Math.Abs(or_[i])));
        }
        WriteWav(path, ol, or_, 0.89 / peak);
        Console.WriteLine("Tamam: " + path + "  (" + (N / (double)SR) + " sn, tepe " + peak.ToString("F3") + ")");
    }

    // Sadece verse/gallop riffi (eski parcanin 18-22. saniyesi), 144 BPM'e yavaslatilmis.
    // 144 BPM * 36 olcu = tam 60 sn.
    public static void RunRiff(string path)
    {
        BPM = 144.0; BEAT = 60.0 / BPM; BARS = 36;
        N = (int)Math.Round(BARS * 4 * BEAT * SR);
        L = new float[N]; R = new float[N]; SL = new float[N]; SR_ = new float[N];

        int[] verse = { 40, 40, 41, 40, 40, 40, 43, 42 }; // E E F E E E G F#
        double gA = 0.085, bA = 0.16;

        Console.WriteLine("Riff...");
        for (int bar = 0; bar < BARS; bar++)
        {
            double b0 = bar * 4;
            int inSec = bar % 8;
            int cycle = bar / 8;
            GallopBar(b0, verse[inSec], gA, bA);
            for (int q = 0; q < 4; q++)
            {
                Kick(b0 + q, 0.48); Kick(b0 + q + 0.5, 0.35); Kick(b0 + q + 0.75, 0.35);
                if (cycle % 2 == 1) Hat(b0 + q, 0.08, true);       // varyasyon: acik hat
                else { Hat(b0 + q, 0.07, false); Hat(b0 + q + 0.5, 0.05, false); }
            }
            bool fill = inSec == 7 || bar == BARS - 1;
            Snare(b0 + 1, 0.36);
            if (fill) for (int k = 0; k < 4; k++) Snare(b0 + 3 + k * 0.25, 0.22 + k * 0.05);
            else Snare(b0 + 3, 0.36);
            if (inSec == 0) Crash(b0, 0.13);
        }

        Console.WriteLine("Reverb...");
        float[] wl = Reverb(SL, 0), wr = Reverb(SR_, 23);
        Console.WriteLine("Mix + yazma...");
        double peak = 0;
        double[] ol = new double[N], or_ = new double[N];
        for (int i = 0; i < N; i++)
        {
            ol[i] = Math.Tanh((L[i] + wl[i] * 0.8) * 1.2);
            or_[i] = Math.Tanh((R[i] + wr[i] * 0.8) * 1.2);
            peak = Math.Max(peak, Math.Max(Math.Abs(ol[i]), Math.Abs(or_[i])));
        }
        WriteWav(path, ol, or_, 0.89 / peak);
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
