namespace SaemDesk.Services.Platform;

public interface ICryptoService
{
    byte[] Protect(byte[] data);
    byte[] Unprotect(byte[] data);
}
