// FarToHell - kilic savurma (whoosh) ses efekti ureticisi.
// Her varyasyon: hizla yukselip inen bant geciren hava sesi + kalin govde ugultusu
// + cok hafif metal bicak tinisi, soldan saga stereo gecis.
using System;
using System.IO;

public static class SwordSwing
{
    const int SR = 44100;

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

    // dur: savurma suresi, fLow/fPeak: hava sesinin frekans araligi, body: kalin govde miktari
    // Metal bicak tinisi: inharmonik kismilar (celik cubuk benzeri)
    static readonly double[] RingRatio = { 1.0, 1.47, 2.09, 2.71, 3.36 };
    static readonly double[] RingAmp = { 1.0, 0.7, 0.5, 0.35, 0.25 };

    static void Make(string path, int seed, double dur, double fLow, double fPeak, double body, double blade, double ringHz)
    {
        Random rng = new Random(seed);
        double tail = 0.35;   // metal tinisi savurmadan sonra da biraz cinlasin
        int n = (int)((dur + tail) * SR);
        double[] l = new double[n], r = new double[n];
        BP bp = new BP();
        double lp1 = 0, lp2 = 0, aLp = 1 - Math.Exp(-2 * Math.PI * 380.0 / SR);
        double peak = 0, ringEnv = 0, ringDecay = Math.Exp(-7.0 / SR);
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / SR;
            double x = Math.Min(t / dur, 1.0);
            // tepe noktasi ~%40'ta: hizlanip yavaslayan kilic
            double e = Math.Pow(Math.Sin(Math.PI * Math.Pow(x, 0.75)), 2);
            if (t > dur) e = 0;
            double fc = fLow + (fPeak - fLow) * e;
            if ((i & 15) == 0) bp.Set(fc, 2.0);

            double noise = rng.NextDouble() * 2 - 1;
            double air = bp.P(noise) * e;

            lp1 += aLp * (noise - lp1); lp2 += aLp * (lp1 - lp2);
            double low = lp2 * 6.0 * Math.Pow(e, 1.5) * body;

            // tini zarfi: savurmayla hizla yukselir, sonra yavasca soner
            ringEnv = e > ringEnv ? ringEnv + (e - ringEnv) * 0.01 : ringEnv * ringDecay;
            double dop = 1 + 0.03 * (e - 0.5);
            double ring = 0;
            for (int p = 0; p < RingRatio.Length; p++)
            {
                double shimmer = 1 + 0.15 * Math.Sin(2 * Math.PI * (9 + p * 3) * t);
                ring += RingAmp[p] * shimmer * Math.Sin(2 * Math.PI * ringHz * RingRatio[p] * dop * t);
            }
            ring *= ringEnv * blade;

            double v = air + low + ring;
            // son 30 ms yumusak kapanis
            double end = (double)(n - i) / SR;
            if (end < 0.03) v *= end / 0.03;

            double pan = -0.6 + 1.2 * x;
            double a = (pan + 1) * Math.PI / 4;
            l[i] = v * Math.Cos(a); r[i] = v * Math.Sin(a);
            peak = Math.Max(peak, Math.Max(Math.Abs(l[i]), Math.Abs(r[i])));
        }
        WriteWav(path, l, r, 0.9 / peak);
        Console.WriteLine("Tamam: " + path + "  (" + ((double)n / SR).ToString("F2") + " sn)");
    }

    // Kulaga hos versiyon: yumusak zarf, genis/yumusak hava bandi, tiz cizirti kesilmis,
    // uyumlu (harmonik) ve kisa bir celik "sing" tinisi, hafif oda yankisi.
    static void MakeSoft(string path, int seed, double dur, double fLow, double fPeak, double body, double blade, double ringHz)
    {
        MakeSoft(path, seed, dur, fLow, fPeak, body, blade, ringHz, 0.85);
    }

    static void MakeSoft(string path, int seed, double dur, double fLow, double fPeak, double body, double blade, double ringHz, double level)
    {
        Random rng = new Random(seed);
        double tail = 0.25;
        int n = (int)((dur + tail) * SR);
        double[] dl = new double[n], dr = new double[n];
        BP bp = new BP();
        double lp1 = 0, lp2 = 0, aLp = 1 - Math.Exp(-2 * Math.PI * 420.0 / SR);
        double ringEnv = 0, ringDecay = Math.Exp(-11.0 / SR);
        double[] rr = { 1.0, 2.0, 3.0 };
        double[] ra = { 1.0, 0.28, 0.1 };
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / SR;
            double x = Math.Min(t / dur, 1.0);
            // yumusak, hafif one egik can egrisi
            double e = t < dur ? Math.Pow(Math.Sin(Math.PI * Math.Pow(x, 0.8)), 2.5) : 0;
            double fc = fLow + (fPeak - fLow) * e;
            if ((i & 15) == 0) bp.Set(fc, 1.0);

            double noise = rng.NextDouble() * 2 - 1;
            double air = bp.P(noise) * e;
            lp1 += aLp * (noise - lp1); lp2 += aLp * (lp1 - lp2);
            double low = lp2 * 6.0 * Math.Pow(e, 1.5) * body;

            ringEnv = e > ringEnv ? ringEnv + (e - ringEnv) * 0.004 : ringEnv * ringDecay;
            double ring = 0;
            for (int p = 0; p < rr.Length; p++)
                ring += ra[p] * Math.Sin(2 * Math.PI * ringHz * rr[p] * t);
            ring *= ringEnv * ringEnv * blade;

            double v = air + low + ring;
            double pan = -0.5 + 1.0 * x;
            double a = (pan + 1) * Math.PI / 4;
            dl[i] = v * Math.Cos(a); dr[i] = v * Math.Sin(a);
        }

        // hafif oda yankisi (erken yansimalar) + tiz cizirtiyi kesen alcak geciren
        double[] l = new double[n], r = new double[n];
        int[] dly = { (int)(0.019 * SR), (int)(0.031 * SR), (int)(0.047 * SR) };
        double[] g = { 0.22, 0.16, 0.1 };
        double aHi = 1 - Math.Exp(-2 * Math.PI * 6500.0 / SR), fl = 0, fr = 0, peak = 0;
        for (int i = 0; i < n; i++)
        {
            double sl = dl[i], sr = dr[i];
            for (int k = 0; k < dly.Length; k++)
                if (i - dly[k] >= 0) { sl += g[k] * dr[i - dly[k]]; sr += g[k] * dl[i - dly[k]]; }
            fl += aHi * (sl - fl); fr += aHi * (sr - fr);
            double end = (double)(n - i) / SR, fade = end < 0.04 ? end / 0.04 : 1;
            l[i] = fl * fade; r[i] = fr * fade;
            peak = Math.Max(peak, Math.Max(Math.Abs(l[i]), Math.Abs(r[i])));
        }
        WriteWav(path, l, r, level / peak);
        Console.WriteLine("Tamam: " + path + "  (" + ((double)n / SR).ToString("F2") + " sn)");
    }

    // En sade versiyon: tek katman filtreli gurultu, yumusak zarf. Govde, tini, yanki, stereo gecis yok.
    static void MakePlain(string path, int seed, double dur, double fLow, double fPeak, double level)
    {
        Random rng = new Random(seed);
        int n = (int)((dur + 0.04) * SR);
        double[] m = new double[n];
        BP bp = new BP();
        double aHi = 1 - Math.Exp(-2 * Math.PI * 5000.0 / SR), f = 0, peak = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / SR;
            double e = t < dur ? Math.Pow(Math.Sin(Math.PI * t / dur), 2) : 0;
            if ((i & 15) == 0) bp.Set(fLow + (fPeak - fLow) * e, 0.9);
            f += aHi * (bp.P(rng.NextDouble() * 2 - 1) * e - f);
            m[i] = f;
            peak = Math.Max(peak, Math.Abs(f));
        }
        WriteWav(path, m, m, level / peak);
        Console.WriteLine("Tamam: " + path + "  (" + ((double)n / SR).ToString("F2") + " sn)");
    }

    public static void Run(string dir)
    {
        // Oyunda kullanilan: tek, sade hava sesi. Cesitlilik oyunda rastgele perde ile saglanir.
        Directory.CreateDirectory(dir);
        MakePlain(Path.Combine(dir, "Swing.wav"), 11, 0.28, 300, 1400, 0.55);

        // Hafif hava + govde + yanki versiyonu (yedek, oyunda yuklenmez)
        string air = Path.Combine(Path.GetDirectoryName(dir), "SwordSwingAir");
        Directory.CreateDirectory(air);
        MakeSoft(Path.Combine(air, "Swing1.wav"), 11, 0.30, 350, 1800, 0.6, 0, 0, 0.55);
        MakeSoft(Path.Combine(air, "Swing2.wav"), 22, 0.34, 320, 1600, 0.7, 0, 0, 0.55);
        MakeSoft(Path.Combine(air, "Swing3.wav"), 33, 0.27, 380, 2000, 0.5, 0, 0, 0.55);

        // Yumusak + celik tinili versiyon (yedek, oyunda yuklenmez)
        string soft = Path.Combine(Path.GetDirectoryName(dir), "SwordSwingSoftRing");
        Directory.CreateDirectory(soft);
        MakeSoft(Path.Combine(soft, "Swing1.wav"), 11, 0.30, 350, 2000, 0.6, 0.03, 1318.5);  // E6
        MakeSoft(Path.Combine(soft, "Swing2.wav"), 22, 0.34, 320, 1800, 0.7, 0.028, 1174.7); // D6
        MakeSoft(Path.Combine(soft, "Swing3.wav"), 33, 0.27, 380, 2200, 0.5, 0.032, 1568.0); // G6

        // Onceki metalik versiyon (yedek, oyunda yuklenmez)
        string metal = Path.Combine(Path.GetDirectoryName(dir), "SwordSwingMetallic");
        Directory.CreateDirectory(metal);
        Make(Path.Combine(metal, "Swing1.wav"), 11, 0.28, 450, 2600, 0.45, 0.05, 1650);
        Make(Path.Combine(metal, "Swing2.wav"), 22, 0.33, 400, 2300, 0.55, 0.045, 1480);
        Make(Path.Combine(metal, "Swing3.wav"), 33, 0.24, 520, 3000, 0.35, 0.055, 1850);
    }

    static void WriteWav(string path, double[] l, double[] r, double g)
    {
        int n = l.Length;
        using (BinaryWriter w = new BinaryWriter(File.Create(path)))
        {
            int dataLen = n * 4;
            w.Write(new char[] { 'R', 'I', 'F', 'F' }); w.Write(36 + dataLen);
            w.Write(new char[] { 'W', 'A', 'V', 'E' });
            w.Write(new char[] { 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)2);
            w.Write(SR); w.Write(SR * 4); w.Write((short)4); w.Write((short)16);
            w.Write(new char[] { 'd', 'a', 't', 'a' }); w.Write(dataLen);
            for (int i = 0; i < n; i++)
            {
                w.Write((short)Math.Round(Math.Max(-1, Math.Min(1, l[i] * g)) * 32767));
                w.Write((short)Math.Round(Math.Max(-1, Math.Min(1, r[i] * g)) * 32767));
            }
        }
    }
}
