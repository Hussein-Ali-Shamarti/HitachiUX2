namespace HitachiUX2.Exceptions;

/// <summary>Basisklasse for alle skriverfeil.</summary>
public class PrinterException(string message) : Exception(message);

/// <summary>Kastes når skriveren er offline eller ikke klar.</summary>
public class PrinterOfflineException(string message) : PrinterException(message);

/// <summary>Kastes når ingen respons mottas innen tidsgrensen.</summary>
public class PrinterTimeoutException(string message) : PrinterException(message);

/// <summary>Kastes når skriveren svarer med NAK — ugyldig data eller opptatt.</summary>
public class PrinterNakException(string message) : PrinterException(message);
