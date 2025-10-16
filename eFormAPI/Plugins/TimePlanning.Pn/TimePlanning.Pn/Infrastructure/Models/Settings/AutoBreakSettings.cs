namespace TimePlanning.Pn.Infrastructure.Models.Settings;

public class AutoBreakSettings
{
    public Monday Monday { get; set; }
    public Tuesday Tuesday { get; set; }
    public Wednesday Wednesday { get; set; }
    public Thursday Thursday { get; set; }
    public Friday Friday { get; set; }
    public Saturday Saturday { get; set; }
    public Sunday Sunday { get; set; }
}

public class Day
{
    public int BreakMinutesDivider { get; set; }
    public int BreakMinutesPrDivider { get; set; }
    public int BreakMinutesUpperLimit { get; set; }

}

public class Monday : Day
{

}

public class Tuesday : Day
{

}

public class Wednesday : Day
{

}

public class Thursday : Day
{

}

public class Friday : Day
{

}

public class Saturday : Day
{

}

public class Sunday : Day
{

}