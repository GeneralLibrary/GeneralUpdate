// This entrypoint deliberately accepts no authorization until the production
// Host protocol and its process/lease binding have been implemented.
Console.Error.WriteLine("Controlled worker unavailable: production Host authorization is not implemented.");
return 2;
