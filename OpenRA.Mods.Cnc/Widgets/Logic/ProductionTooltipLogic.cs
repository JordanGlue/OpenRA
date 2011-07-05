#region Copyright & License Information
/*
 * Copyright 2007-2011 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made 
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using OpenRA.Support;
using OpenRA.Widgets;

namespace OpenRA.Mods.Cnc.Widgets.Logic
{
	public class ProductionTooltipLogic
	{
		[ObjectCreator.UseCtor]
		public ProductionTooltipLogic([ObjectCreator.Param] Widget widget,
		                         [ObjectCreator.Param] ProductionPaletteWidget palette)
		{
			widget.IsVisible = () => palette.TooltipActor != null;
			widget.GetWidget<LabelWidget>("NAME").GetText = () => palette.TooltipActor;
		}
	}
}
