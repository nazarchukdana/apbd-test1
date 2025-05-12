namespace Test1.Models;

public class VisitCreateDTO
{
    public int VisitId { get; set; }
    public int ClientId { get; set; }
    public string MechanicLicenceNumber { get; set; }
    public List<VisitServiceCreateDTO> Services { get; set; }
}