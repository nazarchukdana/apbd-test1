using Test1.Models;

namespace Test1.Services;

public interface IDbService
{
    Task<VisitRequestDTO> GetVisit(int id);
    Task<int> AddVisit(VisitCreateDTO visit);
}