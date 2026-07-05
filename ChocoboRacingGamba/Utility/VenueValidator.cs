using System;
using System.Text;
using System.Text.RegularExpressions;

namespace ChocoboRacing.Utility;

internal static class VenueValidator
{
    public const int MaxNameLength = 40;
    public const int MaxImageDimension = 2048;

    private static readonly Regex UrlLike = new(
        @"https?://|www\.|\b[\w-]+\.(com|net|org|io|gg|co|uk|tv|me|xyz|info|app|dev|link|gov|edu)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ImageUrl = new(
        @"^https?://[^\s]+\.(jpe?g|png|webp)(\?[^\s]*)?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static (bool Ok, string Error) ValidateName(string? name)
    {
        var v = (name ?? string.Empty).Trim();
        if (v.Length == 0) return (true, string.Empty);
        if (v.Length > MaxNameLength) return (false, $"Venue name must be {MaxNameLength} characters or fewer.");
        if (v.IndexOf('<') >= 0 || v.IndexOf('>') >= 0) return (false, "Venue name cannot contain HTML.");
        if (UrlLike.IsMatch(v)) return (false, "Venue name cannot contain a website link.");
        return (true, string.Empty);
    }

    public static (bool Ok, string Error) ValidateImageUrl(string? url)
    {
        var v = (url ?? string.Empty).Trim();
        if (v.Length == 0) return (true, string.Empty);
        if (!Uri.TryCreate(v, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return (false, "Venue image must be a direct http(s) image link.");
        if (uri.Host.Contains("imgur", StringComparison.OrdinalIgnoreCase))
            return (false, "Imgur links cannot be used as the UK cannot view Imgur images. Please host the image elsewhere.");
        if (!ImageUrl.IsMatch(v))
            return (false, "Venue image must be a .jpg, .png or .webp link.");
        return (true, string.Empty);
    }

    public static bool TryReadSize(byte[] data, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (data == null || data.Length < 24) return false;

        if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
        {
            width = Be32(data, 16);
            height = Be32(data, 20);
            return width > 0 && height > 0;
        }

        if (data[0] == 0xFF && data[1] == 0xD8)
            return TryJpeg(data, out width, out height);

        if (data.Length >= 30 && data[0] == (byte)'R' && data[1] == (byte)'I' && data[2] == (byte)'F' && data[3] == (byte)'F'
            && data[8] == (byte)'W' && data[9] == (byte)'E' && data[10] == (byte)'B' && data[11] == (byte)'P')
            return TryWebp(data, out width, out height);

        return false;
    }

    private static int Be32(byte[] d, int i) => (d[i] << 24) | (d[i + 1] << 16) | (d[i + 2] << 8) | d[i + 3];

    private static bool TryJpeg(byte[] d, out int width, out int height)
    {
        width = 0;
        height = 0;
        var p = 2;
        while (p + 9 < d.Length)
        {
            if (d[p] != 0xFF) { p++; continue; }
            var marker = d[p + 1];
            p += 2;
            if (marker == 0xD8 || marker == 0xD9 || (marker >= 0xD0 && marker <= 0xD7) || marker == 0x01) continue;
            if (p + 1 >= d.Length) break;
            var len = (d[p] << 8) | d[p + 1];
            if (len < 2) break;
            var isSof = marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
            if (isSof)
            {
                if (p + 7 >= d.Length) break;
                height = (d[p + 3] << 8) | d[p + 4];
                width = (d[p + 5] << 8) | d[p + 6];
                return width > 0 && height > 0;
            }
            p += len;
        }
        return false;
    }

    private static bool TryWebp(byte[] d, out int width, out int height)
    {
        width = 0;
        height = 0;
        var fourcc = Encoding.ASCII.GetString(d, 12, 4);
        if (fourcc == "VP8 ")
        {
            if (d.Length < 30 || d[23] != 0x9D || d[24] != 0x01 || d[25] != 0x2A) return false;
            width = (d[26] | (d[27] << 8)) & 0x3FFF;
            height = (d[28] | (d[29] << 8)) & 0x3FFF;
            return width > 0 && height > 0;
        }
        if (fourcc == "VP8L")
        {
            if (d.Length < 25 || d[20] != 0x2F) return false;
            var bits = d[21] | (d[22] << 8) | (d[23] << 16) | (d[24] << 24);
            width = (bits & 0x3FFF) + 1;
            height = ((bits >> 14) & 0x3FFF) + 1;
            return true;
        }
        if (fourcc == "VP8X")
        {
            if (d.Length < 30) return false;
            width = (d[24] | (d[25] << 8) | (d[26] << 16)) + 1;
            height = (d[27] | (d[28] << 8) | (d[29] << 16)) + 1;
            return true;
        }
        return false;
    }
}
