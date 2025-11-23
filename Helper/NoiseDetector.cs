using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Accord.Statistics;

namespace HamBot.Helper
{
	public static class NoiseDetector
	{

		//chatgpt special lmao
		public static double GetEntropy(byte[] imageData)
		{
			if (imageData == null) throw new ArgumentNullException(nameof(imageData));

			// Load image (ImageSharp handles many formats)
			using var image = Image.Load<Rgba32>(imageData);

			int width = image.Width;
			int height = image.Height;
			long totalPixels = (long)width * height;

			// Build histogram (0..255 luminance)
			long[] histogram = new long[256];

			// Use ProcessPixelRows which is stable across ImageSharp versions
			image.ProcessPixelRows(accessor =>
			{
				for (int y = 0; y < height; y++)
				{
					var rowSpan = accessor.GetRowSpan(y); // PixelRowSpan<Rgba32>
					for (int x = 0; x < width; x++)
					{
						Rgba32 px = rowSpan[x];

						// Compute linear-ish luminance using BT.709 coefficients
						// Pixel components are 0..255
						double lum = 0.2126 * px.R + 0.7152 * px.G + 0.0722 * px.B;
						int li = (int)Math.Round(lum);
						if (li < 0) li = 0;
						else if (li > 255) li = 255;
						histogram[li]++;
					}
				}
			});

			// Convert histogram -> probabilities
			double[] p = new double[256];
			for (int i = 0; i < 256; i++)
				p[i] = histogram[i] / (double)totalPixels;

			// Compute Shannon entropy in bits: -sum p * log2(p) (skip p==0)
			double entropy = 0.0;
			for (int i = 0; i < 256; i++)
			{
				double pi = p[i];
				if (pi <= 0.0) continue;
				entropy -= pi * Math.Log(pi, 2.0);
			}

			// Optionally: expose entropy for debugging:
			Console.WriteLine($"Entropy = {entropy:F3} bits");

			return entropy;
		}
	}
}
