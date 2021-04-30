using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using FileBadger.Properties;

namespace FileBadger
{
    [Localizable(true)]
    static class Hash
    {
        private static readonly MD5 Md5Engine = MD5.Create();

        public static byte[] Compute(byte[] dataForHash)
        {
            Debug.Assert(dataForHash != null, Resources.Error_Compute_Hash_Compute_is_called_with_an_invalid_parameter);
            return Md5Engine.ComputeHash(dataForHash);
        }
    }
}
