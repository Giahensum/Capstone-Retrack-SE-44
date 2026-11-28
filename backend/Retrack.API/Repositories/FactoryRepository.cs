using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories
{
    public class FactoryRepository : IFactoryRepository
    {
        private readonly AppDbContext _db;

        public FactoryRepository(AppDbContext db) => _db = db;

        public Task<Factory?> GetByOwnerIdAsync(Guid ownerId) =>
            _db.Factories.FirstOrDefaultAsync(f => f.OwnerId == ownerId);

        /// <summary>Chỉ thêm vào context, chưa lưu. Lưu cùng một lần cùng User để đảm bảo nguyên tử.</summary>
        public Task AddAsync(Factory factory)
        {
            _db.Factories.Add(factory);
            return Task.CompletedTask;
        }
    }
}
