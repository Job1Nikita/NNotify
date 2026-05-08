import { SignJWT, jwtVerify } from "jose";

interface AccessTokenInput {
  userId: string;
  username: string;
  sessionId: string;
  deviceId: string;
  issuer: string;
  audience: string;
  ttlSeconds: number;
  secret: string;
}

const encoder = new TextEncoder();

export async function createAccessToken(input: AccessTokenInput): Promise<string> {
  const key = encoder.encode(input.secret);

  return new SignJWT({
    usr: input.username,
    sid: input.sessionId,
    did: input.deviceId,
    typ: "access"
  })
    .setProtectedHeader({ alg: "HS512", typ: "JWT" })
    .setSubject(input.userId)
    .setIssuer(input.issuer)
    .setAudience(input.audience)
    .setIssuedAt()
    .setExpirationTime(`${input.ttlSeconds}s`)
    .sign(key);
}

export interface VerifiedAccessToken {
  userId: string;
  username: string;
  sessionId: string;
  deviceId: string;
}

export async function verifyAccessToken(
  token: string,
  issuer: string,
  audience: string,
  secret: string
): Promise<VerifiedAccessToken> {
  const key = encoder.encode(secret);
  const result = await jwtVerify(token, key, {
    issuer,
    audience
  });

  if (result.payload.typ !== "access") {
    throw new Error("Invalid token type.");
  }

  const userId = result.payload.sub;
  const username = result.payload.usr;
  const sessionId = result.payload.sid;
  const deviceId = result.payload.did;

  if (
    typeof userId !== "string" ||
    typeof username !== "string" ||
    typeof sessionId !== "string" ||
    typeof deviceId !== "string"
  ) {
    throw new Error("Invalid token payload.");
  }

  return { userId, username, sessionId, deviceId };
}
