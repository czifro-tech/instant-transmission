// This is an extension to .Net Core native libraries
// This is so that OSX specific features can be 
//  added to IO operations


/**
 * Constants that are OSX specific for opening files
 */
enum
{
    PAL_O_NONBLOCK = 0x3000, // No Buffering, push right to disk
};
