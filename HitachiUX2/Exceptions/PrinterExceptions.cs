namespace HitachiUX2.Exceptions;

public class PrinterException(string message) : Exception(message);
public class PrinterOfflineException(string message) : PrinterException(message);
public class PrinterTimeoutException(string message) : PrinterException(message);
public class PrinterNakException(string message) : PrinterException(message);
