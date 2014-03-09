﻿//-----------------------------------------------------------------------
// <copyright file="SymmetricCryptographicKey.cs" company="Andrew Arnott">
//     Copyright (c) Andrew Arnott. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace PCLCrypto
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Java.Security;
    using Javax.Crypto;
    using Javax.Crypto.Spec;
    using Validation;

    /// <summary>
    /// A .NET Framework implementation of <see cref="ICryptographicKey"/> for use with symmetric algorithms.
    /// </summary>
    internal class SymmetricCryptographicKey : CryptographicKey, ICryptographicKey
    {
        /// <summary>
        /// The symmetric algorithm.
        /// </summary>
        private readonly SymmetricAlgorithm algorithm;

        /// <summary>
        /// The symmetric key.
        /// </summary>
        private readonly IKey key;

        /// <summary>
        /// Initializes a new instance of the <see cref="SymmetricCryptographicKey" /> class.
        /// </summary>
        /// <param name="algorithm">The algorithm.</param>
        /// <param name="keyMaterial">The key.</param>
        internal SymmetricCryptographicKey(SymmetricAlgorithm algorithm, byte[] keyMaterial)
        {
            Requires.NotNull(keyMaterial, "keyMaterial");

            this.algorithm = algorithm;
            this.key = new SecretKeySpec(keyMaterial, SymmetricKeyAlgorithmProviderFactory.GetTitleName(this.algorithm));
        }

        /// <inheritdoc />
        public int KeySize
        {
            get { throw new NotImplementedException(); }
        }

        /// <inheritdoc />
        public byte[] Export(CryptographicPrivateKeyBlobType blobType = CryptographicPrivateKeyBlobType.Pkcs8RawPrivateKeyInfo)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public byte[] ExportPublicKey(CryptographicPublicKeyBlobType blobType = CryptographicPublicKeyBlobType.X509SubjectPublicKeyInfo)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        protected internal override byte[] Encrypt(byte[] data, byte[] iv)
        {
            using (var cipher = this.GetInitializedCipher(CipherMode.EncryptMode, iv))
            {
                return cipher.DoFinal(data);
            }
        }

        /// <inheritdoc />
        protected internal override byte[] Decrypt(byte[] data, byte[] iv)
        {
            using (var cipher = this.GetInitializedCipher(CipherMode.DecryptMode, iv))
            {
                return cipher.DoFinal(data);
            }
        }

        /// <inheritdoc />
        protected internal override ICryptoTransform CreateEncryptor(byte[] iv)
        {
            return new CryptoTransformAdaptor(this.GetInitializedCipher(CipherMode.EncryptMode, iv));
        }

        /// <inheritdoc />
        protected internal override ICryptoTransform CreateDecryptor(byte[] iv)
        {
            return new CryptoTransformAdaptor(this.GetInitializedCipher(CipherMode.DecryptMode, iv));
        }

        /// <summary>
        /// Gets the padding substring to include in the string
        /// passed to <see cref="Cipher.GetInstance(string)"/>
        /// </summary>
        /// <param name="algorithm">The algorithm.</param>
        /// <returns>A value such as "PKCS7Padding", or <c>null</c> if no padding.</returns>
        private static string GetPadding(SymmetricAlgorithm algorithm)
        {
            switch (SymmetricKeyAlgorithmProviderFactory.GetPadding(algorithm))
            {
                case SymmetricKeyAlgorithmProviderFactory.SymmetricAlgorithmPadding.None:
                    return null;
                case SymmetricKeyAlgorithmProviderFactory.SymmetricAlgorithmPadding.PKCS7:
                    return "PKCS7Padding";
                default:
                    throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Creates a zero IV buffer.
        /// </summary>
        /// <param name="iv">The IV supplied by the caller.</param>
        /// <param name="cipher">The cipher, if already created.</param>
        /// <returns>
        ///   <paramref name="iv" /> if not null; otherwise a zero-filled buffer.
        /// </returns>
        private byte[] ThisOrDefaultIV(byte[] iv, Cipher cipher = null)
        {
            if (iv != null)
            {
                return iv;
            }
            else if (cipher != null)
            {
                return new byte[cipher.BlockSize];
            }
            else
            {
                using (cipher = Cipher.GetInstance(SymmetricKeyAlgorithmProviderFactory.GetTitleName(this.algorithm)))
                {
                    return new byte[cipher.BlockSize];
                }
            }
        }

        /// <summary>
        /// Initializes a new cipher.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <param name="iv">The initialization vector to use.</param>
        /// <returns>
        /// The initialized cipher.
        /// </returns>
        private Cipher GetInitializedCipher(CipherMode mode, byte[] iv)
        {
            var cipherName = this.GetCipherAcquisitionName();

            var cipher = Cipher.GetInstance(cipherName.ToString());
            using (var ivspec = new IvParameterSpec(this.ThisOrDefaultIV(iv, cipher)))
            {
                try
                {
                    cipher.Init(mode, this.key, ivspec);
                }
                catch (Java.Security.InvalidKeyException ex)
                {
                    throw new ArgumentException(ex.Message, ex);
                }

                return cipher;
            }
        }

        /// <summary>
        /// Assembles a string to pass to <see cref="Cipher.GetInstance(string)"/>
        /// that identifies the algorithm, block mode and padding.
        /// </summary>
        /// <returns>A string such as "AES/CBC/PKCS7Padding</returns>
        private StringBuilder GetCipherAcquisitionName()
        {
            var cipherName = new StringBuilder(SymmetricKeyAlgorithmProviderFactory.GetTitleName(this.algorithm));
            cipherName.Append("/");
            cipherName.Append(SymmetricKeyAlgorithmProviderFactory.GetMode(this.algorithm));
            string paddingString = GetPadding(this.algorithm);
            if (paddingString != null)
            {
                cipherName.Append("/");
                cipherName.Append(paddingString);
            }

            return cipherName;
        }

        /// <summary>
        /// Adapts a platform Cipher to the PCL interface.
        /// </summary>
        private class CryptoTransformAdaptor : ICryptoTransform
        {
            /// <summary>
            /// The platform transform.
            /// </summary>
            private readonly Cipher transform;

            /// <summary>
            /// Initializes a new instance of the <see cref="CryptoTransformAdaptor"/> class.
            /// </summary>
            /// <param name="transform">The transform.</param>
            internal CryptoTransformAdaptor(Cipher transform)
            {
                Requires.NotNull(transform, "transform");
                this.transform = transform;
            }

            /// <inheritdoc />
            public bool CanReuseTransform
            {
                get { return false; }
            }

            /// <inheritdoc />
            public bool CanTransformMultipleBlocks
            {
                get { return true; }
            }

            /// <inheritdoc />
            public int InputBlockSize
            {
                get { return this.transform.BlockSize; }
            }

            /// <inheritdoc />
            public int OutputBlockSize
            {
                get { return this.transform.GetOutputSize(this.InputBlockSize); }
            }

            /// <inheritdoc />
            public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
            {
                return this.transform.Update(inputBuffer, inputOffset, inputCount, outputBuffer, outputOffset);
            }

            /// <inheritdoc />
            public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
            {
                return this.transform.DoFinal(inputBuffer, inputOffset, inputCount);
            }

            /// <inheritdoc />
            public void Dispose()
            {
                this.transform.Dispose();
            }
        }
    }
}
