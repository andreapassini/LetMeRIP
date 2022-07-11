// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ActorGroup.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

namespace Photon.Hive
{
    public class ActorGroup : List<Actor>
    {
        #region Constructors and Destructors

        public ActorGroup(byte id)
        {
            this.GroupId = id;
        }

        #endregion

        #region Properties

        public byte GroupId { get; private set; }

        #endregion

        #region Publics

        public IEnumerable<Actor> GetExcludedList(Actor actorToExclude)
        {
            return this.Where(actor => actor != actorToExclude);
        }

        #endregion
    }
}