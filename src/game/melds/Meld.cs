
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Godot;
using Rummy.AI;
using Rummy.Util;
using static Rummy.Util.Result;

namespace Rummy.Game;

public interface IMeld : IReadableCardPile
{
    public bool Valid { get; }

    // Would layoff be successful?
    public bool CouldLayOff(Card card);
    public int IndexIfLaidOff(Card card);
    
    public NearMeld AsNear();
}

public abstract class Meld : CardPile, IMeld
{
    public new ReadOnlyCollection<Card> Cards => _cards.ToList().AsReadOnly();

    // Is this a valid (playable) meld?
    public abstract bool Valid { get; }

    public abstract Result<Unit, Unit> LayOff(Card card);
    public abstract void InternalUndoLayOff(Card card);

    // Would layoff be successful?
    public abstract bool CouldLayOff(Card card);
    public abstract int IndexIfLaidOff(Card card);

    // Clone of Meld with current cards and without any current listeners
    public abstract Meld Clone();
    public abstract NearMeld AsNear();

    public abstract event Action<Card> NotifyLaidOff, NotifyLayOffUndone;
}