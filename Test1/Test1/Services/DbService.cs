using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.Data.SqlClient;
using Test1.Exceptions;
using Test1.Models;

namespace Test1.Services;

public class DbService : IDbService
{
    private readonly string _connectionString;

    public DbService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    public async Task<VisitRequestDTO> GetVisit(int id)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand();
        cmd.Connection = conn;
        cmd.CommandText = "select count(*) from Visit where visit_id = @id";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@id", id);
        var result = await cmd.ExecuteScalarAsync();
        if (result == null || (int)result <= 0)
            throw new NotFoundException("Visit not found");
        cmd.CommandText = @"select date, c.first_name, c.last_name, date_of_birth, m.mechanic_id, licence_number
                            from Visit v 
                            join Client c on c.client_id = v.client_id
                            join Mechanic m on v.mechanic_id = m.mechanic_id
                            where v.visit_id = @id";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@id", id);
        VisitRequestDTO? visit = null;
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                visit = new VisitRequestDTO
                {
                    Client = new ClientDTO
                    {
                        FirstName = (string)reader["first_name"],
                        LastName = (string)reader["last_name"],
                        DateOfBirth = (DateTime)reader["date_of_birth"],
                    },
                    Date = (DateTime)reader["date"],
                    Mechanic = new MechanicDTO
                    {
                        MechanicId = (int)reader["mechanic_id"],
                        LicenceNumber = (string)reader["licence_number"],
                    }

                };
            }
        }
        if(visit == null)
            throw new NotFoundException("Visit Not Found");
        cmd.CommandText = @"select name, service_fee
                            from Visit_Service vs
                             join Service s on s.service_id = vs.service_id
                             where vs.visit_id = @id";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@id", id);
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                visit.VisitServices.Add(new VisitServiceDTO
                {
                    Name = (string)reader["name"],
                    ServiceFee = (decimal)reader["service_fee"],
                });
            }
        }
        return visit;
    }

    public async Task<int> AddVisit(VisitCreateDTO visit)
    {
        if (visit.Services.Count == 0)
            throw new BadRequestException("Services should be provided");
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand();
        cmd.Connection = conn;
        await using var transaction = await conn.BeginTransactionAsync() as SqlTransaction;
        cmd.Transaction = transaction;
        
        cmd.CommandText = "select count(*) from Visit where visit_id = @id";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@id", visit.VisitId);
        var result = await cmd.ExecuteScalarAsync();
        if (result != null && (int)result > 0)
            throw new ConflictException("Visit with this id already exists");
        cmd.CommandText = "select count(*) from Client where client_id = @id";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@id", visit.ClientId);
        result = await cmd.ExecuteScalarAsync();
        if(result == null || (int)result <= 0)
            throw new NotFoundException("Client not found");
        cmd.CommandText = "select mechanic_id from Mechanic where licence_number = @licence";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@licence", visit.MechanicLicenceNumber);
        var mechanicId = await cmd.ExecuteScalarAsync();
        if(mechanicId == null || (int)mechanicId <= 0)
            throw new NotFoundException("Mechanic not found");
        try
        {
            cmd.CommandText = @"Insert into Visit(visit_id, client_id, mechanic_id, date)
                                 output inserted.visit_id
                                values (@visitId, @clientId, @mechanicId, @date)";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@visitId", visit.VisitId);
            cmd.Parameters.AddWithValue("@clientId", visit.ClientId);
            cmd.Parameters.AddWithValue("@mechanicId", mechanicId);
            cmd.Parameters.AddWithValue("@date", DateTime.Now);
            var id = await cmd.ExecuteScalarAsync();
            if (id == null || (int)id <= 0)
                throw new Exception("Visit not added");
            foreach (var service in visit.Services)
            {
                cmd.CommandText = "select service_id from Service where name = @name";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@name", service.ServiceName);
                var serviceId = await cmd.ExecuteScalarAsync();
                if (serviceId == null || (int)serviceId <= 0)
                    throw new NotFoundException("Service not found");
                cmd.CommandText = @"insert into Visit_Service(visit_id, service_id, service_fee)
                                        values (@visitId, @serviceId, @serviceFee)";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@visitId", visit.VisitId);
                cmd.Parameters.AddWithValue("@serviceId", serviceId);
                cmd.Parameters.AddWithValue("@serviceFee", service.ServiceFee);
                result = await cmd.ExecuteNonQueryAsync();
                if ((int)result == 0)
                    throw new Exception("Service not added");
            }

            await transaction.CommitAsync();
            return (int)id;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }


    }
}