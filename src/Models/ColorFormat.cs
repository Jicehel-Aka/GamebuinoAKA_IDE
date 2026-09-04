namespace GamebuinoAKA.IDE.Models
{
    /// <summary>
    /// Ordre d'empaquetage des composantes couleur dans le uint16 exporté.
    ///
    /// La lib Gamebuino AKA (gb_ll_lcd.h → lcd_color_rgb) empaquette ainsi :
    ///     u16 = (R>>3) | ((G>>2)<<5) | ((B>>3)<<11)
    /// c.-à-d. ROUGE dans les bits de poids faible, BLEU dans les bits de poids
    /// fort. C'est de l'ordre « BGR565 » au niveau du mot 16 bits.
    /// core/graphics.h le confirme : graphics_make_color(...) -> BGR565.
    ///
    /// C'est donc le format PAR DÉFAUT : un tableau exporté en Bgr565Aka se
    /// blitte tel quel avec graphics_draw_bitmap565() sans inversion R/B.
    ///
    /// Rgb565Std est l'ordre « standard » (R en bits hauts) que d'autres
    /// bibliothèques/outils utilisent ; laissé en option au cas où.
    /// </summary>
    public enum ColorFormat
    {
        /// <summary>Ordre lib AKA : R bits 0-4, G bits 5-10, B bits 11-15. (défaut)</summary>
        Bgr565Aka = 0,

        /// <summary>Ordre standard : R bits 11-15, G bits 5-10, B bits 0-4.</summary>
        Rgb565Std = 1
    }
}
