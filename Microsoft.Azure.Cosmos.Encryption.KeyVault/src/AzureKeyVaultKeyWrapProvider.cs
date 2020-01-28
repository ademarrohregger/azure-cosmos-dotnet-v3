﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption.KeyVault
{
    using System;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides functionality to wrap (encrypt) and unwrap (decrypt) data encryption keys using master keys stored in Azure Key Vault.
    /// See https://docs.microsoft.com/en-us/rest/api/azure/index#register-your-client-application-with-azure-ad for details on registering your application with Azure AD.
    /// The registered application must have the keys/wrapKey and keys/unwrapKey permissions on the Azure Key Vaults that will be used for wrapping and unwrapping data encryption keys
    /// - see https://docs.microsoft.com/en-us/azure/key-vault/about-keys-secrets-and-certificates#key-access-control for details on this.
    /// See https://tbd for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
        class AzureKeyVaultKeyWrapProvider : EncryptionKeyWrapProvider
    {
        private static KeyVaultAccessClientFactory keyVaultAccessClientFactory = new KeyVaultAccessClientFactory();
        private IKeyVaultAccessClient keyVaultAccessClient;
        private TimeSpan rawDekCacheTimeToLive;

        /// <summary>
        /// Creates a new instance of a provider to wrap (encrypt) and unwrap (decrypt) data encryption keys using master keys stored in Azure Key Vault.
        /// </summary>
        /// <param name="clientId">Application (client) ID.</param>
        /// <param name="certificate">A certificate that can be used as secret to prove the application’s identity when requesting a token.</param>
        /// <param name="rawDataEncryptionKeyCacheTimeToLive">
        /// Amount of time the unencrypted form of the data encryption key can be cached on the client before <see cref="UnwrapKeyAsync"/> needs to be called again.
        /// </param>
        public AzureKeyVaultKeyWrapProvider(string clientId, X509Certificate2 certificate, TimeSpan rawDataEncryptionKeyCacheTimeToLive)
        {
            this.keyVaultAccessClient = AzureKeyVaultKeyWrapProvider.keyVaultAccessClientFactory.CreateKeyVaultAccessClient(clientId, certificate);
            this.rawDekCacheTimeToLive = rawDataEncryptionKeyCacheTimeToLive;
        }

        /// <inheritdoc />
        public override async Task<EncryptionKeyUnwrapResult> UnwrapKeyAsync(byte[] wrappedKey, EncryptionKeyWrapMetadata metadata, CancellationToken cancellationToken)
        {
            if(metadata.Type != AzureKeyVaultKeyWrapMetadata.TypeConstant)
            {
                throw new ArgumentException("Invalid metadata", nameof(metadata));
            }

            if(metadata.Algorithm != KeyVaultConstants.RsaOaep256)
            {
                throw new ArgumentException(
                    string.Format("Unknown encryption key wrap algorithm {0}", metadata.Algorithm),
                    nameof(metadata));
            }

            string wrappedKeyString = Convert.ToBase64String(wrappedKey);
            try
            {
                KeyVaultUnwrapResult result = await this.keyVaultAccessClient.UnwrapKeyAsync(wrappedKeyString, new Uri(metadata.Value), cancellationToken);
                return new EncryptionKeyUnwrapResult(Convert.FromBase64String(result.UnwrappedKeyBytesInBase64), this.rawDekCacheTimeToLive);
            }
            catch (KeyVaultAccessException ex)
            {
                if(ex.KeyVaultErrorCode == KeyVaultErrorCode.KeyVaultKeyNotFound)
                {
                    throw new MasterKeyNotFoundException(ex.Message, ex);
                }

                throw;
            }
        }

        /// <inheritdoc />
        public override async Task<EncryptionKeyWrapResult> WrapKeyAsync(byte[] key, EncryptionKeyWrapMetadata metadata, CancellationToken cancellationToken)
        {
            if (metadata.Type != AzureKeyVaultKeyWrapMetadata.TypeConstant)
            {
                throw new ArgumentException("Invalid metadata", nameof(metadata));
            }

            string keyString = Convert.ToBase64String(key);
            try
            {
                KeyVaultWrapResult result = await this.keyVaultAccessClient.WrapKeyAsync(keyString, new Uri(metadata.Value), cancellationToken);
                EncryptionKeyWrapMetadata responseMetadata = new EncryptionKeyWrapMetadata(metadata)
                {
                    Algorithm = KeyVaultConstants.RsaOaep256
                };

                return new EncryptionKeyWrapResult(Convert.FromBase64String(result.WrappedKeyBytesInBase64), responseMetadata);
            }
            catch (KeyVaultAccessException ex)
            {
                if (ex.KeyVaultErrorCode == KeyVaultErrorCode.KeyVaultKeyNotFound)
                {
                    throw new MasterKeyNotFoundException(ex.Message, ex);
                }

                throw;
            }
        }
    }
}