use strict;
use warnings;
use GD;

# Very poor mans input validation (use with caution)
die "Three input images and and output path expected!" if scalar(@ARGV) != 4;

GD::Image->trueColor(1);

my $metal = GD::Image->new($ARGV[0]);
my $rough = GD::Image->new($ARGV[1]);
my $ao = GD::Image->new($ARGV[2]);

die "Dimensions do not match, aborting!" if $ao->width != $metal->width || $ao->height != $metal->height;
die "Dimensions do not match, aborting!" if $ao->width != $rough->width || $ao->height != $rough->height;

my $im = GD::Image->newTrueColor($ao->width, $ao->height);

$im->saveAlpha(0);
$im->alphaBlending(0);

for (my $x = 0; $x < $im->width; $x++)
{
	for (my $y = 0; $y < $im->height; $y++)
	{
		my @m = $metal->rgb($metal->getPixel($x, $y));
		my @s = $rough->rgb($rough->getPixel($x, $y));
		my @o = $ao->rgb($ao->getPixel($x, $y));

		$im->setPixel($x, $y, 
			$im->colorAllocate(
				$m[0], $s[0], $o[1])
		)
	}
}

my $data = $im->png;

warn "Writing ", $ARGV[3], "\n";

open (my $fh, ">", $ARGV[3])
 	or die "Could open output: $!";
binmode $fh;
print $fh $data;
