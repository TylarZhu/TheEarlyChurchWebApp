using Microsoft.AspNetCore.Http;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Domain.DBEntities;
using Domain.Interfaces;
using Domain.APIClass;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("afterGame/[controller]")]
    public class AfterGameController : Controller
    {
        private readonly IGameHistoryService gameHistoryService;
        private readonly IRankingBoardService rankingBoardService;
        
        public AfterGameController(IGameHistoryService gameHistoryService, IRankingBoardService rankingBoardService)
        {
            this.gameHistoryService = gameHistoryService;
            this.rankingBoardService = rankingBoardService;
        }

        [HttpGet("AllGameHistory")]
        public async Task<ActionResult<List<GameHistory>>> getAllGameHistory() =>
            await gameHistoryService.getGameHistories();

        [HttpGet("AllRankingBoard")]
        public async Task<ActionResult<List<RankingBoard>>> getAllRankingBoard() =>
            await rankingBoardService.getRankingBoards();

        [HttpPost("GameHistory")]
        public async Task<IActionResult> postGameHistory([FromBody] GameHistory gameHistory)
        {
            await gameHistoryService.createGameHistory(gameHistory);
            return CreatedAtAction(nameof(postGameHistory), new { id = gameHistory.gameId }, gameHistory);
        }

        [HttpPost("RankingBoard")]
        public async Task<IActionResult> postRankingBoard([FromBody] RankingBoardReceiveFromClient rankingBoard)
        {
            for(int i = 0; i < rankingBoard.winners.Length; i ++)
            {
                await rankingBoardService.createRankingBoard(rankingBoard.winners[i]);
            }
            return CreatedAtAction(nameof(postRankingBoard), rankingBoard.winners);
        }
    }
}
