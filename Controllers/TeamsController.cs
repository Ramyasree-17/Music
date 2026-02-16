using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TunewaveAPIDB1.Repositories;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Controllers
{
    [ApiController]
    [Route("api/teams")]
    [Authorize]
    [Tags("Teams")]
    public class TeamsController : ControllerBase
    {
        private readonly ITeamRepository _teamRepository;

        public TeamsController(ITeamRepository teamRepository)
        {
            _teamRepository = teamRepository;
        }

        [HttpPost]
        public async Task<IActionResult> CreateTeam(CreateTeamDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var teamId = await _teamRepository.CreateTeamAsync(dto, userId);

            return Ok(new
            {
                message = "Team created successfully",
                teamId = teamId
            });
        }

        [HttpPost("member")]
        public async Task<IActionResult> AddMember(AddTeamMemberDto dto)
        {
            await _teamRepository.AddTeamMemberAsync(dto);

            return Ok(new
            {
                message = "Team member added successfully"
            });
        }
    }
}
