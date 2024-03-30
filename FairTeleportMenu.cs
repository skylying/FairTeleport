// Copyright 2020 Jamie Taylor
// Portions Copyright 2016–2019 Pathoschild and other contributors, see NOTICE for license
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace MapTeleport 
{
    /// <summary>
    /// Encapsulates the UI for the to-do list.
    /// </summary>
    public class MapTeleportMenu : IClickableMenu, IDisposable {

        private readonly ModEntry theMod;
        //private readonly ToDoList theList;
        //private List<MenuItem> menuItemList;
        //private ItemConfigMenu? currentItemEditor;

        private readonly TextBox Textbox;
        /// <summary>The maximum pixels to scroll.</summary>
        private int MaxScroll;
        /// <summary>The number of pixels to scroll.</summary>
        private int CurrentScroll;
        /// <summary>Force the CurrentScroll to the bottom (MaxScroll) after rendering all items</summary>
        /// Set after adding an item because adding is asynchronous for farmhands.  Cleared on other actions.
        private bool forceScrollToBottom = false;

        /// <summary>The area where list items are rendered (used to filter mouse clicks)</summary>
        private Rectangle contentArea = Rectangle.Empty;

        /// <summary>spacing between the menu edge and content area</summary>
        private const int gutter = 15;
        /// <summary>width of the content area (not including gutter)</summary>
        private int contentWidth;
        /// <summary>height of the content area (not including gutter)</summary>
        private int contentHeight;

        private Rectangle scrollUpRect;
        private bool scrollUpVisible = false;
        private Rectangle scrollDownRect;
        private bool scrollDownVisible = false;

        public MapTeleportMenu()
        {
            // update size
            this.width = Math.Min(Game1.tileSize * 14, Game1.viewport.Width);
            this.height = Math.Min((int)((float)Sprites.Letter.Sprite.Height / Sprites.Letter.Sprite.Width * this.width), Game1.viewport.Height);
            this.contentWidth = this.width - gutter * 2;
            this.contentHeight = this.height - gutter * 2;

            // update position
            Vector2 origin = Utility.getTopLeftPositionForCenteringOnScreen(this.width, this.height);
            this.xPositionOnScreen = (int)origin.X;
            this.yPositionOnScreen = (int)origin.Y;

            // initialize the scroll button location rectangles
            scrollDownRect = new Rectangle(xPositionOnScreen + gutter, yPositionOnScreen + contentHeight - CommonSprites.Icons.DownArrow.Height, CommonSprites.Icons.DownArrow.Width, CommonSprites.Icons.DownArrow.Height);
            scrollUpRect = new Rectangle(xPositionOnScreen + gutter, scrollDownRect.Top - gutter - CommonSprites.Icons.UpArrow.Height, CommonSprites.Icons.UpArrow.Width, CommonSprites.Icons.UpArrow.Height);

            // create the text box
            this.Textbox = new TextBox(Sprites.Textbox.Sheet, null, Game1.smallFont, Color.Black);
            this.Textbox.TitleText = "Add Waypoint";
            this.Textbox.Selected = true;

            // initialize the list UI and callback
            //theList.OnChanged += OnListChanged;
            //syncMenuItemList();
        }

        public override void draw(SpriteBatch b)
        {
            int x = this.xPositionOnScreen;
            int y = this.yPositionOnScreen;
            float leftOffset = gutter;
            float topOffset = gutter;
            float bodyWidth = this.width - leftOffset - gutter; // same as contentWidth

            Console.WriteLine("Start Drawing");

            // get font
            SpriteFont font = Game1.smallFont;
            float spaceWidth = CommonHelper.GetSpaceWidth(font);

            // draw background and header
            // (This uses a separate sprite batch because it needs to be drawn before the
            // foreground batch, and we can't use the foreground batch because the background is
            // outside the clipping area.)
            using (SpriteBatch backgroundBatch = new SpriteBatch(Game1.graphics.GraphicsDevice)) {
                float scale = this.width / (float)Sprites.Letter.Sprite.Width;
                backgroundBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, null, null);
                backgroundBatch.Draw(Sprites.Letter.Sheet, new Vector2(this.xPositionOnScreen, this.yPositionOnScreen),
                Sprites.Letter.Sprite, Color.White, 0, Vector2.Zero, scale, SpriteEffects.None, 0);

                Vector2 titleSize = backgroundBatch.DrawTextBlock(font, "Fair Teleport", new Vector2(x + leftOffset, y + topOffset), bodyWidth, bold: true);
                Vector2 farmNameSize = backgroundBatch.DrawTextBlock(font, "Waypoint List", new Vector2(x + leftOffset + titleSize.X + spaceWidth, y + topOffset), bodyWidth);
                topOffset += Math.Max(titleSize.Y, farmNameSize.Y);
                
                // if (currentItemEditor == null) {
                //   Vector2 titleSize = backgroundBatch.DrawTextBlock(font, I18n.Menu_List_TitleBoldPart(), new Vector2(x + leftOffset, y + topOffset), bodyWidth, bold: true);
                //   Vector2 farmNameSize = backgroundBatch.DrawTextBlock(font, I18n.Menu_List_TitleRest(farmName: Game1.player.farmName.Value), new Vector2(x + leftOffset + titleSize.X + spaceWidth, y + topOffset), bodyWidth);
                //   topOffset += Math.Max(titleSize.Y, farmNameSize.Y);
                // } else {
                //     Vector2 titleSize = backgroundBatch.DrawTextBlock(font, I18n.Menu_Edit_Title(), new Vector2(x + leftOffset, y + topOffset), bodyWidth, bold: true);
                //     topOffset += titleSize.Y;
                // }

                this.Textbox.X = x + (int)leftOffset;
                this.Textbox.Y = y + (int)topOffset;
                this.Textbox.Width = (int)bodyWidth;
                //this.Textbox.Draw(backgroundBatch); // 這行會爆掉 這應該就是 <input> , 我不需要吧
                topOffset += this.Textbox.Height;

                backgroundBatch.End();
            }

            topOffset += gutter;
            int headerHeight = (int)topOffset;

            // draw foreground
            // (This uses a separate sprite batch to set a clipping area for scrolling.)
            using (SpriteBatch contentBatch = new SpriteBatch(Game1.graphics.GraphicsDevice)) {
                GraphicsDevice device = Game1.graphics.GraphicsDevice;
                Rectangle prevScissorRectangle = device.ScissorRectangle;
                try {
                    // begin draw
                    device.ScissorRectangle = new Rectangle(x + gutter, y + headerHeight, contentWidth, contentHeight - headerHeight);
                    contentArea = device.ScissorRectangle;
                    contentBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, null, new RasterizerState { ScissorTestEnable = true });

                    //// scroll view
                    this.CurrentScroll = Math.Max(0, this.CurrentScroll); // don't scroll past top
                    this.CurrentScroll = Math.Min(this.MaxScroll, this.CurrentScroll); // don't scroll past bottom
                    topOffset -= this.CurrentScroll; // scrolled down == move text up

                    int mouseX = Game1.getMouseX();
                    int mouseY = Game1.getMouseY();

                    // // draw fields
                    // {
                    //     if (currentItemEditor == null) {
                    //         foreach (MenuItem item in this.menuItemList) {
                    //             var objSize = item.Draw(contentBatch, x + (int)leftOffset, y + (int)topOffset, (int)bodyWidth, mouseX, mouseY);
                    //             topOffset += objSize.Y;
                    //         }
                    //     } else {
                    //         var objSize = currentItemEditor.Draw(contentBatch, x + (int)leftOffset, y + (int)topOffset, (int)bodyWidth, (int)contentHeight - headerHeight, mouseX, mouseY);
                    //         topOffset += objSize.Y;
                    //     }
                    // }

                    // // update max scroll
                    // this.MaxScroll = Math.Max(0, (int)(topOffset - contentHeight + this.CurrentScroll));
                    // if (forceScrollToBottom) {
                    //     this.CurrentScroll = this.MaxScroll;
                    // }

                    // draw scroll icons
                    // scrollUpVisible = this.MaxScroll > 0 && this.CurrentScroll > 0;
                    // scrollDownVisible = this.MaxScroll > 0 && this.CurrentScroll < this.MaxScroll;
                    // if (scrollUpVisible)
                    //     contentBatch.DrawSprite(CommonSprites.Icons.Sheet, CommonSprites.Icons.UpArrow, scrollUpRect.X, scrollUpRect.Y, null, scrollUpRect.Contains(mouseX, mouseY) ? 1.1f : 1.0f);
                    // if (scrollDownVisible)
                    //     contentBatch.DrawSprite(CommonSprites.Icons.Sheet, CommonSprites.Icons.DownArrow, scrollDownRect.X, scrollDownRect.Y, null, scrollDownRect.Contains(mouseX, mouseY) ? 1.1f : 1.0f);

                    // end draw
                    contentBatch.End();
                } finally {
                    device.ScissorRectangle = prevScissorRectangle;
                }


                this.drawMouse(Game1.spriteBatch);
            }
        }
        
        public void Dispose() {
            //this.theList.OnChanged -= OnListChanged;
        }
    }

    // Based on https://github.com/Pathoschild/StardewMods/blob/develop/LookupAnything/Components/Sprites.cs
    /// <summary>Simplifies access to the game's sprite sheets.</summary>
    /// <remarks>Each sprite is represented by a rectangle, which specifies the coordinates and dimensions of the image in the sprite sheet.</remarks>
    internal static class Sprites {
        /*********
        ** Accessors
        *********/
        /// <summary>Sprites used to draw a letter.</summary>
        public static class Letter {
            /// <summary>The sprite sheet containing the letter sprites.</summary>
            public static Texture2D Sheet => Game1.content.Load<Texture2D>("LooseSprites\\letterBG");

            /// <summary>The letter background (including edges and corners).</summary>
            public static readonly Rectangle Sprite = new Rectangle(0, 0, 320, 180);

            /// <summary>The notebook paper letter background (including edges and corners).</summary>
            public static readonly Rectangle NotebookSprite = new Rectangle(320, 0, 320, 180);
        }

        /// <summary>Sprites used to draw a textbox.</summary>
        public static class Textbox {
            /// <summary>The sprite sheet containing the textbox sprites.</summary>
            public static Texture2D Sheet => Game1.content.Load<Texture2D>("LooseSprites\\textBox");
        }
    }
}
