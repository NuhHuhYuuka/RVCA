using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace SecurityData.Services
{
    public class KeyExchangeService : IDisposable
    {
        private readonly ECDiffieHellmanCng _ecdh;
        private readonly ConcurrentDictionary<string, byte[]> _sessionKeys;

        public KeyExchangeService()
        {
            _ecdh = new ECDiffieHellmanCng
            {
                KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash,
                HashAlgorithm = CngAlgorithm.Sha256
            };

            _sessionKeys = new ConcurrentDictionary<string, byte[]>();
        }

        public string ExportPublicKey()
        {
            return Convert.ToBase64String(_ecdh.PublicKey.ToByteArray());
        }

        public byte[] DeriveSessionKeyFromPeer(string peerName, string peerPublicKeyBase64)
        {
            byte[] peerPublicKeyBytes = Convert.FromBase64String(peerPublicKeyBase64);

            using (var peerKey = ECDiffieHellmanCngPublicKey.FromByteArray(
                peerPublicKeyBytes,
                CngKeyBlobFormat.EccPublicBlob))
            {
                byte[] sharedSecret = _ecdh.DeriveKeyMaterial(peerKey);
                _sessionKeys[peerName] = sharedSecret;
                return sharedSecret;
            }
        }

        public bool HasSessionKey(string peerName)
        {
            return _sessionKeys.ContainsKey(peerName);
        }

        public byte[] GetSessionKey(string peerName)
        {
            if (!_sessionKeys.TryGetValue(peerName, out var key))
                throw new InvalidOperationException($"Chưa có session key với peer: {peerName}");

            return key;
        }

        public void RemoveSessionKey(string peerName)
        {
            _sessionKeys.TryRemove(peerName, out _);
        }

        public void Dispose()
        {
            _ecdh?.Dispose();
        }
    }
}