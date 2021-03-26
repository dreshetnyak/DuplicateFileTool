using System.Diagnostics;
using System.Security.Cryptography;

namespace FileBadger
{
    static class Hash
    {
        private static readonly MD5 Md5Engine = MD5.Create();

        public static byte[] Compute(byte[] dataForHash)
        {
            Debug.Assert(dataForHash != null, "Hash.Compute is called with an invalid parameter 'dataForHash' that is null");
            return Md5Engine.ComputeHash(dataForHash);
        }
    }
}
