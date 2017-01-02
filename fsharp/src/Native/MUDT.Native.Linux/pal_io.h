// This is an extension to .Net Core native libraries
// This is so that Linux specific features can be 
//  added to IO operations


/**
 * Constants that are Linux specific for opening files
 */
enum
{
    PAL_O_DIRECT = 0x2000, // No Buffering, push right to disk
};
