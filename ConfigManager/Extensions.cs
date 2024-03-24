using System;
using System.IO;

namespace ConfigManager
{
    public static class Extensions
    {
        private const int READ_BYTES = sizeof(long);

        public static int CompareModified(this FileInfo file, FileInfo other)
        {
            switch (file)
            {
                case null when other is null:
                    return 0;
                case not null when other is null:
                    return 1;
                case null when other is not null:
                    return -1;
                default:
                    break;
            }

            // Shouldn't be null here.
            _ = file ?? throw new ArgumentNullException(nameof(file));
            _ = other ?? throw new ArgumentNullException(nameof(other));

            bool fileExists = file.Exists;
            bool otherExists = other.Exists;

            switch (fileExists)
            {
                case false when !otherExists:
                    return 0;
                case true when !otherExists:
                    return 1;
                case false when otherExists:
                    return -1;
                default:
                    break;
            }

            // Same file.
            if (file.FullName.Equals(other.FullName, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            long fileModified = file.LastWriteTime.Ticks;
            long otherModified = other.LastWriteTime.Ticks;

            if (fileModified == otherModified)
            {
                return 0;
            }

            long fileLength = file.Length;
            long otherLength = other.Length;

            if (fileLength != otherLength)
            {
                if (fileModified > otherModified)
                {
                    return 1;
                }

                if (otherModified > fileModified)
                {
                    return -1;
                }
            }

            int iterations = (int)Math.Ceiling((double)fileLength / READ_BYTES);
            int i = 0;
            bool filesAreEqual = true;

            byte[] bytes1 = new byte[READ_BYTES];
            byte[] bytes2 = new byte[READ_BYTES];

            using FileStream stream1 = file.OpenRead();
            using FileStream stream2 = other.OpenRead();

            while (filesAreEqual && i < iterations)
            {
                stream1.Read(bytes1, 0, READ_BYTES);
                stream2.Read(bytes2, 0, READ_BYTES);

                if (BitConverter.ToInt64(bytes1, 0) != BitConverter.ToInt64(bytes2, 0))
                {
                    filesAreEqual = false;
                }

                i++;
            }

            if (filesAreEqual)
            {
                return 0;
            }

            return (fileModified > otherModified) ? 1 : -1;
        }

        public static bool LessThanOrEqualToAny(this DateTime dateTime, params DateTime[] dateTimes)
        {
            _ = dateTimes ?? throw new ArgumentNullException(nameof(dateTimes));

            bool result = false;
            int i = 0;

            while (!result && i < dateTimes.Length)
            {
                result = dateTimes[i] >= dateTime;
                i++;
            }

            return result;
        }

        public static bool FileNameContains(this FileInfo fileInfo, string searchText)
        {
            _ = fileInfo ?? throw new ArgumentNullException(nameof(fileInfo));

            bool result;

            try
            {
                if (string.IsNullOrWhiteSpace(searchText) || searchText.Trim() == string.Empty)
                {
                    result = true;
                }
                else if (searchText.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
                {
                    result = Path.GetFileNameWithoutExtension(fileInfo.Name).Contains(searchText, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    result = false;
                }
            }
            catch
            {
                result = false;
            }

            return result;
        }
    }
}
