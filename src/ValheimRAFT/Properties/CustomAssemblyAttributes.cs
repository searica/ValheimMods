namespace Properties;

[System.AttributeUsage(System.AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
sealed class SentryDSN : System.Attribute
{
  public string Dsn { get; }

  public SentryDSN(string sentryDsn)
  {
    Dsn = sentryDsn;
  }
}