namespace FileBadger.Converters
{
    internal static class DataConversion
    {
        public static string BytesLengthToString(this long length)
        {
            const long kilobyteSize = 1_024;
            const long megabyteSize = 1_048_576;
            const long gigabyteSize = 1_073_741_824;

            if (length >= gigabyteSize)
                return $"{length / megabyteSize:N2} GB";

            if (length >= megabyteSize)
                return $"{length / megabyteSize} MB";

            return length >= kilobyteSize
                ? $"{length / kilobyteSize} KB"
                : $"{length:N0} Bytes";
        }
    }
}
