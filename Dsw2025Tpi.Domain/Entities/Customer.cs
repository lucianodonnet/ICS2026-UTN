using System;
using System.ComponentModel.DataAnnotations;

namespace Dsw2025Tpi.Domain.Entities;

public class Customer : EntityBase
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    // Constructor vacío para Entity Framework
    protected Customer() { }

    // Constructor modificado para aceptar el ID específico
    public Customer(Guid id, string name, string email, string? phoneNumber = "N/A")
        : base(id) // Llamamos al constructor nuevo de EntityBase
    {
        Name = name;
        Email = email;
        PhoneNumber = phoneNumber;
    }
}