using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Legal;

namespace WalletWasabi.Backend.Controllers;

/// <summary>
/// To acquire Wasabi software related data.
/// </summary>
[Produces("application/json")]
[Route("api/v" + Constants.BackendMajorVersion + "/[controller]")]
public class WasabiController : ControllerBase
{
	/// <summary>
	/// Gets the latest legal documents.
	/// </summary>
	/// <returns>Returns the legal documents.</returns>
	/// <response code="200">Returns the legal documents.</response>
	[HttpGet("legaldocuments")]
	[ProducesResponseType(typeof(byte[]), 200)]
	public async Task<IActionResult> GetLegalDocumentsAsync(string? id, CancellationToken cancellationToken)
	{
		string filePath;

		// we give back the EmbeddedFilePathForGingerWallet for all cases
		switch (id)
		{
			case "gingerwallet":
				filePath = LegalDocuments.EmbeddedFilePathForGingerWallet;
				break;

			case "ww2":
				filePath = LegalDocuments.EmbeddedFilePathForGingerWallet;
				break;

			case null:
				filePath = LegalDocuments.EmbeddedFilePathForGingerWallet; // If the document id is null, then the request comes from WW 1.0 client.
				break;

			default:
				return NotFound();
		}

		var content = await System.IO.File.ReadAllBytesAsync(filePath, cancellationToken);
		return File(content, "text/plain");
	}
}
