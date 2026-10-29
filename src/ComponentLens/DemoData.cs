namespace ComponentLens;

public static class DemoData
{
    public static ScanResult Create()
    {
        Category resistors = new("res", "Resistors", "Passive Components/Resistors");
        Category capacitors = new("cap", "Capacitors", "Passive Components/Capacitors");
        Category semiconductors = new("ic", "Integrated Circuits", "Semiconductors/Integrated Circuits");
        Category connectors = new("con", "Connectors", "Electromechanical/Connectors");
        Category misc = new("misc", "Unsorted", "Unsorted");
        Component C(string id, string name, Category category, string manufacturer, string mpn, string description, bool parameters = false) =>
            new(id, name, "01", description, category, "Components/" + category.Name,
                parameters ? [new("Manufacturer", manufacturer), new("Manufacturer Part Number", mpn)] : [], [],
                parameters ? [] : [new(manufacturer, mpn)]);
        Component[] a = [
            C("demo-a-1", "CMP-001247", resistors, "Yageo", "RC0603FR-0710KL", "10 kΩ · 1% · 0603"),
            C("demo-a-2", "CMP-000892", capacitors, "Murata", "GRM188R71H104KA93D", "100 nF · 50 V · X7R · 0603"),
            C("demo-a-3", "CMP-002401", semiconductors, "Texas Instruments", "TPS62160DSGR", "DC/DC buck · 1 A · 3–17 V", true),
            C("demo-a-4", "CMP-001251", resistors, "YAGEO", "RC0603FR-0710KL", "10 kΩ · alternate library entry", true),
            C("demo-a-5", "CMP-003106", connectors, "Molex", "0022232021", "Header · 2 contacts · 2.54 mm"),
            C("demo-a-6", "CMP-004002", misc, "", "", "Generic mounting hole"),
            C("demo-a-7", "CMP-001700", resistors, "Vishay", "CRCW060310K0FKEA", "10 kΩ · 1% · 0603")
        ];
        Component[] b = [
            C("demo-b-1", "RES-000312", resistors, "Yageo", "RC0603FR-0710KL", "10 kΩ · 1% · 0603"),
            C("demo-b-2", "CAP-000041", capacitors, "MURATA", "GRM188R71H104KA93D", "100 nF · 50 V · X7R · 0603", true),
            C("demo-b-3", "IC-000079", semiconductors, "Texas Instruments", "TPS62160DSGR", "DC/DC buck · 1 A · 3–17 V"),
            C("demo-b-4", "CON-000502", connectors, "Molex", "0022232021", "Header · 2 contacts · 2.54 mm"),
            C("demo-b-5", "CAP-000132", capacitors, "KEMET", "C0603C105K4PACTU", "1 µF · 16 V · X5R"),
            C("demo-b-6", "CAP-000159", capacitors, "KEMET", "C0603C105K4PACTU", "1 µF · second library entry", true),
            C("demo-b-7", "MISC-000016", misc, "", "", "Test point")
        ];
        return DuplicateDetector.Compare(new("a", "Engineering · Europe", "https://demo-a.365.altium.com/api/graphql", a.Length, [resistors, capacitors, semiconductors, connectors, misc]), a,
            new("b", "Global Library", "https://demo-b.365.altium.com/api/graphql", b.Length, [resistors, capacitors, semiconductors, connectors, misc]), b, true, true);
    }
}
