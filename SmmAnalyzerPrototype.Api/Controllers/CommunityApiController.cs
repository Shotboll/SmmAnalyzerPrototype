using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmmAnalyzerPrototype.Data.Data;
using SmmAnalyzerPrototype.Data.Models;
using SmmAnalyzerPrototype.Data.Models.DTO.Community;

namespace SmmAnalyzerPrototype.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class CommunityApiController : Controller
    {
        private readonly AppDbContext _context;

        public CommunityApiController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<List<CommunityDto>>> GetAll()
        {
            var communities = await _context.Communities.ToListAsync();
            return Ok(communities.Select(MapToDto));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CommunityDto>> GetById(Guid id)
        {
            var community = await _context.Communities.FindAsync(id);
            if (community == null) return NotFound();
            return Ok(MapToDto(community));
        }

        [HttpPost]
        public async Task<ActionResult<CommunityDto>> Create([FromBody] CreateCommunityRequest request)
        {
            var community = new Community
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                TargetAudience = request.TargetAudience,
                StyleProfile = request.StyleProfile
            };
            _context.Communities.Add(community);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = community.Id }, MapToDto(community));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCommunityRequest request)
        {
            var community = await _context.Communities.FindAsync(id);
            if (community == null) return NotFound();

            community.Name = request.Name;
            community.TargetAudience = request.TargetAudience;
            community.StyleProfile = request.StyleProfile;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var community = await _context.Communities.FindAsync(id);
            if (community == null) return NotFound();

            _context.Communities.Remove(community);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static CommunityDto MapToDto(Community c) => new()
        {
            Id = c.Id,
            Name = c.Name,
            TargetAudience = c.TargetAudience,
            StyleProfile = c.StyleProfile
        };
    }
}
