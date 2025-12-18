using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace GameEngine.Security
{
    /// <summary>
    /// GLI-19 Control Program Integrity verifier. Ensures that every critical executable,
    /// library, and configuration file matches the reference digest before the engine comes online.
    /// </summary>
    public sealed class ControlProgramVerifier
    {
        private readonly string _manifestPath;
        private readonly HashAlgorithmName _hashAlgorithm;
        private readonly JsonSerializerOptions _serializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        public ControlProgramVerifier(string manifestPath, HashAlgorithmName? hashAlgorithm = null)
        {
            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                throw new ArgumentException("Manifest path must be provided.", nameof(manifestPath));
            }

            _manifestPath = Path.GetFullPath(manifestPath);
            _hashAlgorithm = hashAlgorithm ?? HashAlgorithmName.SHA256;
        }

        /// <summary>
        /// Executes integrity verification and throws if any component hash deviates from the manifest.
        /// </summary>
        public void Verify()
        {
            if (!File.Exists(_manifestPath))
            {
                throw new FileNotFoundException($"Control program manifest was not found at `{_manifestPath}`.");
            }

            using var stream = File.OpenRead(_manifestPath);
            var manifest = JsonSerializer.Deserialize<ControlProgramManifest>(stream, _serializerOptions)
                           ?? throw new InvalidOperationException("Manifest content could not be parsed.");

            if (manifest.Components.Count == 0)
            {
                throw new InvalidOperationException("Manifest does not contain any components to verify.");
            }

            foreach (var component in manifest.Components)
            {
                ValidateComponent(component);
            }
        }

        private void ValidateComponent(ManifestComponent component)
        {
            var fullPath = Path.GetFullPath(component.Path);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Critical component is missing: `{fullPath}`.");
            }

            var actualHash = ComputeHash(fullPath);
            if (!actualHash.Equals(component.ExpectedHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Integrity violation detected for `{fullPath}`. Expected {component.ExpectedHash}, computed {actualHash}.");
            }
        }

        private string ComputeHash(string path)
        {
            using HashAlgorithm algorithm = _hashAlgorithm.Name switch
            {
                nameof(HashAlgorithmName.SHA512) => SHA512.Create(),
                nameof(HashAlgorithmName.SHA384) => SHA384.Create(),
                nameof(HashAlgorithmName.SHA1) => SHA1.Create(),
                _ => SHA256.Create()
            };

            using var stream = File.OpenRead(path);
            var hashBytes = algorithm.ComputeHash(stream);
            return Convert.ToHexString(hashBytes);
        }

        /// <summary>
        /// Strongly typed representation of the manifest JSON.
        /// </summary>
        public sealed record ControlProgramManifest(IReadOnlyList<ManifestComponent> Components);

        public sealed record ManifestComponent(string Path, string ExpectedHash);
    }
}

