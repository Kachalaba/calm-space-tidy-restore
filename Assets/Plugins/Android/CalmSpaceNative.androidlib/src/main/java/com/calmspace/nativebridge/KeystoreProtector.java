package com.calmspace.nativebridge;

import android.os.Build;
import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;

import java.nio.ByteBuffer;
import java.security.Key;
import java.security.KeyStore;
import java.util.Arrays;

import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

public final class KeystoreProtector {
    private static final String KEYSTORE_PROVIDER = "AndroidKeyStore";
    private static final String TRANSFORMATION = "AES/GCM/NoPadding";
    private static final byte ENVELOPE_VERSION = 1;
    private static final int NONCE_BYTES = 12;
    private static final int TAG_BITS = 128;
    private static final int TAG_BYTES = TAG_BITS / 8;
    private static final Object KEY_LOCK = new Object();

    private KeystoreProtector() {
    }

    public static boolean isSupported() {
        return Build.VERSION.SDK_INT >= Build.VERSION_CODES.N;
    }

    public static byte[] protect(
            String alias,
            byte[] plaintext,
            byte[] associatedData) {
        if (!isSupported() ||
                alias == null ||
                alias.length() == 0 ||
                plaintext == null) {
            return null;
        }

        byte[] nonce = null;
        byte[] ciphertextAndTag = null;
        try {
            SecretKey key = getOrCreateKey(alias);

            Cipher cipher = Cipher.getInstance(TRANSFORMATION);
            // Android Keystore keys created with randomized encryption
            // required must let the provider generate the IV.
            cipher.init(Cipher.ENCRYPT_MODE, key);
            nonce = cipher.getIV();
            if (nonce == null || nonce.length != NONCE_BYTES) {
                return null;
            }

            if (associatedData != null && associatedData.length > 0) {
                cipher.updateAAD(associatedData);
            }

            ciphertextAndTag = cipher.doFinal(plaintext);
            ByteBuffer envelope = ByteBuffer.allocate(
                    1 + NONCE_BYTES + ciphertextAndTag.length);
            envelope.put(ENVELOPE_VERSION);
            envelope.put(nonce);
            envelope.put(ciphertextAndTag);
            return envelope.array();
        } catch (Exception exception) {
            return null;
        } finally {
            if (nonce != null) {
                Arrays.fill(nonce, (byte) 0);
            }
            if (ciphertextAndTag != null) {
                Arrays.fill(ciphertextAndTag, (byte) 0);
            }
        }
    }

    public static byte[] unprotect(
            String alias,
            byte[] envelope,
            byte[] associatedData) {
        if (!isSupported() ||
                alias == null ||
                alias.length() == 0 ||
                envelope == null ||
                envelope.length < 1 + NONCE_BYTES + TAG_BYTES ||
                envelope[0] != ENVELOPE_VERSION) {
            return null;
        }

        byte[] nonce = null;
        byte[] ciphertextAndTag = null;
        try {
            SecretKey key = getExistingKey(alias);
            if (key == null) {
                return null;
            }

            nonce = Arrays.copyOfRange(
                    envelope,
                    1,
                    1 + NONCE_BYTES);
            ciphertextAndTag = Arrays.copyOfRange(
                    envelope,
                    1 + NONCE_BYTES,
                    envelope.length);

            Cipher cipher = Cipher.getInstance(TRANSFORMATION);
            cipher.init(
                    Cipher.DECRYPT_MODE,
                    key,
                    new GCMParameterSpec(TAG_BITS, nonce));
            if (associatedData != null && associatedData.length > 0) {
                cipher.updateAAD(associatedData);
            }

            return cipher.doFinal(ciphertextAndTag);
        } catch (Exception exception) {
            return null;
        } finally {
            if (nonce != null) {
                Arrays.fill(nonce, (byte) 0);
            }
            if (ciphertextAndTag != null) {
                Arrays.fill(ciphertextAndTag, (byte) 0);
            }
        }
    }

    private static SecretKey getOrCreateKey(String alias)
            throws Exception {
        synchronized (KEY_LOCK) {
            SecretKey existing = getExistingKey(alias);
            if (existing != null) {
                return existing;
            }

            KeyGenerator generator = KeyGenerator.getInstance(
                    KeyProperties.KEY_ALGORITHM_AES,
                    KEYSTORE_PROVIDER);
            KeyGenParameterSpec specification =
                    new KeyGenParameterSpec.Builder(
                            alias,
                            KeyProperties.PURPOSE_ENCRYPT |
                                    KeyProperties.PURPOSE_DECRYPT)
                            .setBlockModes(
                                    KeyProperties.BLOCK_MODE_GCM)
                            .setEncryptionPaddings(
                                    KeyProperties.ENCRYPTION_PADDING_NONE)
                            .setKeySize(256)
                            .setRandomizedEncryptionRequired(true)
                            .setUserAuthenticationRequired(false)
                            .build();
            generator.init(specification);
            return generator.generateKey();
        }
    }

    private static SecretKey getExistingKey(String alias)
            throws Exception {
        KeyStore keyStore = KeyStore.getInstance(KEYSTORE_PROVIDER);
        keyStore.load(null);
        Key key = keyStore.getKey(alias, null);
        return key instanceof SecretKey ? (SecretKey) key : null;
    }
}
