namespace Portal {
	public enum PortalId {
		Blue = 0,
		Orange = 1
	}

	public static class PortalIdExtensions {
		public static PortalId Other(this PortalId id) {
			return id == PortalId.Blue ? PortalId.Orange : PortalId.Blue;
		}
	}
}
