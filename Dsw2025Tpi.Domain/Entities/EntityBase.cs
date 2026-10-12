using System;
using System.ComponentModel.DataAnnotations;

namespace Dsw2025Tpi.Domain.Entities;

public abstract class EntityBase
{
    [Key]
    public Guid Id { get; protected set; } // 'protected set' para que las hijas puedan asignarlo

    // Constructor vacío (genera ID nuevo)
    protected EntityBase()
    {
        Id = Guid.NewGuid();
    }

    // NUEVO: Constructor para forzar un ID específico
    protected EntityBase(Guid id)
    {
        Id = id;
    }
}