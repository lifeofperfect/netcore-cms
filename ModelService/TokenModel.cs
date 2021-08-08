using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace ModelService
{
    public class TokenModel
    {
        [Key]
        public int Id { get; set; }
        [Required]
        // the client id where it comes from
        public string ClientId { get; set; }
        [Required]
        //value of the tokens
        public string Value { get; set; }
        [Required]
        //get the token created date
        public DateTime CreatedDate { get; set; }
        [Required]
        //userid it was issued to
        public string UserId { get; set; }
        [Required]
        public DateTime LastModifiedDate { get; set; }
        [Required]
        public DateTime ExpiryTime { get; set; }
        [Required]
        public string EncryptionKeyRt { get; set; }
        [Required]
        public string EncryptionKeyJwt { get; set; }
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

    }
}
