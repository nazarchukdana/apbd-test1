namespace Test1.Models;

public class VisitRequestDTO
{
    public DateTime Date { get; set; }
    public ClientDTO? Client { get; set; }
    public MechanicDTO? Mechanic { get; set; }
    public List<VisitServiceDTO> VisitServices { get; set; } = new List<VisitServiceDTO>();
}