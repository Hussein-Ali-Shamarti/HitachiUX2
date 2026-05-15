// Rask tilkoblingstest — kjør med: dotnet run
// Endre SKRIVER_IP til skriverens faktiske IP-adresse.

using HitachiUX2;
using HitachiUX2.Exceptions;

const string SKRIVER_IP   = "192.168.0.250";
const int    SKRIVER_PORT = 1024;

Console.WriteLine(new string('=', 55));
Console.WriteLine("  Hitachi UX2-D160W SDK — Tilkoblingstest");
Console.WriteLine(new string('=', 55));
Console.WriteLine($"Kobler til {SKRIVER_IP}:{SKRIVER_PORT} ...");

using var skriver = new HitachiUX2Printer(SKRIVER_IP, SKRIVER_PORT);

try
{
    skriver.Connect();
    Console.WriteLine("✓ TCP-tilkobling OK");

    skriver.SyncTime();
    Console.WriteLine("✓ Klokke synkronisert");

    skriver.SendPrintContent(new()
    {
        [2] = DateTime.Now.ToString("dd.MM.yy")
    });
    Console.WriteLine("✓ Print-innhold sendt");

    Console.WriteLine("\nAlt OK — skriveren er klar til bruk.");
}
catch (PrinterOfflineException ex)
{
    Console.WriteLine($"\nSKRIVER OFFLINE: {ex.Message}");
}
catch (PrinterNakException ex)
{
    Console.WriteLine($"\nSKRIVERFEIL (NAK): {ex.Message}");
}
catch (PrinterTimeoutException ex)
{
    Console.WriteLine($"\nTIDSAVBRUDD: {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"\nUVENTET FEIL: {ex.GetType().Name}: {ex.Message}");
}
finally
{
    skriver.Disconnect();
}
