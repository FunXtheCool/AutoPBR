plugins {
    java
    application
}

repositories {
    mavenCentral()
}

val parityRoot = file("../../tools/minecraft-parity/26.1.2")

dependencies {
    implementation(files(parityRoot.resolve("client.jar")))
    implementation(
        fileTree(parityRoot.resolve("libraries")) {
            include("**/*.jar")
        },
    )
}

application {
    mainClass.set("autopbr.reference.GeometryReferenceBake")
}

// Pinned client.jar is Java 25 (class file 69). Run Gradle itself on JDK 21; toolchain supplies JDK 25 for compile/run.
java {
    toolchain {
        languageVersion.set(JavaLanguageVersion.of(25))
    }
}
