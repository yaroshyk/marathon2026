using System.ComponentModel.DataAnnotations;

namespace APIHandler.Models.GroupReservation;

public class SubmitGroupPreOrderRequest
{
    [Required(AllowEmptyStrings = false)]
    public string ReservationId { get; set; } = string.Empty;
}
