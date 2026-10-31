using System;

namespace Convergence.Chain
{
    /// <summary>
    /// THE TEST WALLET (Tuning.Testing.DemoWalletAddress) - the one account that holds the gear
    /// made in development (GearOwnership.AuthoredHeld; the user's call, 2026-10-31). Matched on
    /// the STAKE credential, not the whole address: a wallet hands back different payment
    /// addresses under one stake key, and every one of them is the same account.
    /// </summary>
    public static class TestWallet
    {
        /// <summary>Whether <paramref name="address"/> is the test wallet. Only a proven address
        /// should be asked - see GameBootstrap.DevGearHeld.</summary>
        public static bool Is(string address)
        {
            if (string.IsNullOrEmpty(address)) return false;
            if (address == Core.Tuning.Testing.DemoWalletAddress) return true;
            var mine = StakePart(address);
            var test = StakePart(Core.Tuning.Testing.DemoWalletAddress);
            return mine != null && test != null && mine.AsSpan().SequenceEqual(test);
        }

        /// <summary>A base address's stake credential (its last 28 bytes), or null for anything
        /// else - an enterprise address has none, and is matched only whole.</summary>
        static byte[] StakePart(string bech32)
        {
            var data = Bech32Bytes(bech32);
            // Base addresses (header types 0-3): header + 28 payment + 28 stake.
            if (data == null || data.Length != 57 || (data[0] >> 4) > 3) return null;
            return data[29..];
        }

        const string Charset = "qpzry9x8gf2tvdw0s3jn54khce6mua7l";

        /// <summary>The payload of a bech32 string, checksum not verified (it is compared with a
        /// constant, never trusted for anything).</summary>
        static byte[] Bech32Bytes(string s)
        {
            int sep = s.LastIndexOf('1');
            if (sep < 1 || s.Length - sep - 1 < 6) return null;
            var five = new int[s.Length - sep - 1 - 6];
            for (int i = 0; i < five.Length; i++)
            {
                int v = Charset.IndexOf(char.ToLowerInvariant(s[sep + 1 + i]));
                if (v < 0) return null;
                five[i] = v;
            }
            var bytes = new System.Collections.Generic.List<byte>();
            int acc = 0, bits = 0;
            foreach (int v in five)
            {
                acc = (acc << 5) | v;
                bits += 5;
                if (bits >= 8) { bits -= 8; bytes.Add((byte)((acc >> bits) & 0xff)); }
            }
            return bytes.ToArray();
        }
    }
}
