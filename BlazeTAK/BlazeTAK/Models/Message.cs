using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BlazeTAK.Data.Models;

[Table("Messages")]
public class Message
{
    [Key]
    public Guid Id { get; set; }

    [Column("Content")]
    public string content { get; set; } = string.Empty;

    public DateTime TimeStamp { get; set; } //always UTC (Npgsql timestamptz)

    //nullable so deleting a user account keeps the message
    [ForeignKey(nameof(Sender))]
    public Guid? SenderId { get; set; }
    [DeleteBehavior(DeleteBehavior.SetNull)]
    public VirtUser? Sender { get; set; }
    
    [ForeignKey(nameof(Recipient))]
    public Guid? RecipientId { get; set; }
    [DeleteBehavior(DeleteBehavior.SetNull)]
    public VirtUser? Recipient { get; set; }
    //TODO recipient (VirtUser of user / unit / group?)
}
